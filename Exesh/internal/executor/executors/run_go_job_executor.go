package executors

import (
	"bytes"
	"context"
	"errors"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/jobs"
	"exesh/internal/domain/execution/results"
	"exesh/internal/runtime"
	"fmt"
	"io"
	"log/slog"
	"time"
)

type RunGoJobExecutor struct {
	log            *slog.Logger
	inputProvider  inputProvider
	outputProvider outputProvider
	runtime        runtime.Runtime
}

func NewRunGoJobExecutor(log *slog.Logger, inputProvider inputProvider, outputProvider outputProvider, rt runtime.Runtime) *RunGoJobExecutor {
	return &RunGoJobExecutor{
		log:            log,
		inputProvider:  inputProvider,
		outputProvider: outputProvider,
		runtime:        rt,
	}
}

func (e *RunGoJobExecutor) SupportsType(jobType execution.JobType) bool {
	return jobType == execution.RunGoJobType
}

func (e *RunGoJobExecutor) Execute(ctx context.Context, job execution.Job) execution.Result {
	errorResult := func(err error) execution.Result {
		return results.RunResult{
			ResultDetails: execution.ResultDetails{
				ID:     job.GetID(),
				Type:   execution.RunResult,
				DoneAt: time.Now(),
				Error:  err.Error(),
			},
		}
	}

	runtimeErrorResult := func() execution.Result {
		return results.RunResult{
			ResultDetails: execution.ResultDetails{
				ID:     job.GetID(),
				Type:   execution.RunResult,
				DoneAt: time.Now(),
			},
			Status: results.RunStatusRE,
		}
	}

	okResult := func() execution.Result {
		return results.RunResult{
			ResultDetails: execution.ResultDetails{
				ID:     job.GetID(),
				Type:   execution.RunResult,
				DoneAt: time.Now(),
			},
			Status: results.RunStatusOK,
		}
	}

	okResultWithOutput := func(output string) execution.Result {
		return results.RunResult{
			ResultDetails: execution.ResultDetails{
				ID:     job.GetID(),
				Type:   execution.RunResult,
				DoneAt: time.Now(),
			},
			Status:    results.RunStatusOK,
			HasOutput: true,
			Output:    output,
		}
	}

	tlResult := func() execution.Result {
		return results.RunResult{
			ResultDetails: execution.ResultDetails{
				ID:     job.GetID(),
				Type:   execution.RunResult,
				DoneAt: time.Now(),
			},
			Status:    results.RunStatusTL,
			HasOutput: false,
		}
	}

	mlResult := func() execution.Result {
		return results.RunResult{
			ResultDetails: execution.ResultDetails{
				ID:     job.GetID(),
				Type:   execution.RunResult,
				DoneAt: time.Now(),
			},
			Status: results.RunStatusML,
		}
	}

	if job.GetType() != execution.RunGoJobType {
		return errorResult(fmt.Errorf("unsupported job type %s for %s executor", job.GetType(), execution.RunGoJobType))
	}
	runGoJob := job.(*jobs.RunGoJob)

	_, unlock, err := e.inputProvider.Locate(ctx, runGoJob.Code)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate code input: %w", err))
	}
	defer unlock()

	_, unlock, err = e.inputProvider.Locate(ctx, runGoJob.RunInput)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate run_input input: %w", err))
	}
	defer unlock()

	var compiledCodeLocation string
	compiledCodeLocation, unlock, err = e.inputProvider.Locate(ctx, runGoJob.Code)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate code input: %w", err))
	}
	defer unlock()

	var runInput io.Reader
	runInput, unlock, err = e.inputProvider.Read(ctx, runGoJob.RunInput)
	if err != nil {
		return errorResult(fmt.Errorf("failed to read run_input input: %w", err))
	}
	defer unlock()

	runOutput, commit, abort, err := e.outputProvider.Create(ctx, runGoJob.RunOutput)
	if err != nil {
		return errorResult(fmt.Errorf("failed to create run_output output: %w", err))
	}

	stderr := bytes.NewBuffer(nil)
	err = e.runtime.Execute(ctx,
		[]string{"/a.out"},
		runtime.ExecuteParams{
			// TODO: Limits
			Limits: runtime.Limits{
				Memory: runtime.MemoryLimit(int64(runGoJob.MemoryLimit) * int64(runtime.Megabyte)),
				Time:   runtime.TimeLimit(int64(runGoJob.TimeLimit) * int64(time.Millisecond)),
			},
			InFiles: []runtime.File{{OutsideLocation: compiledCodeLocation, InsideLocation: "/a.out"}},
			Stderr:  stderr,
			Stdin:   runInput,
			Stdout:  runOutput,
		})
	if err != nil {
		e.log.Error("execute binary in runtime error", slog.Any("err", err))
		if errors.Is(err, runtime.ErrTimeout) {
			return tlResult()
		}
		if errors.Is(err, runtime.ErrOutOfMemory) {
			return mlResult()
		}
		return runtimeErrorResult()
	}

	e.log.Info("command ok")

	if err = commit(); err != nil {
		_ = abort()
		return errorResult(fmt.Errorf("failed to commit output creation: %w", err))
	}

	if !runGoJob.ShowOutput {
		return okResult()
	}

	runOutputReader, unlock, err := e.outputProvider.Read(ctx, runGoJob.RunOutput)
	if err != nil {
		return errorResult(fmt.Errorf("failed to open run output: %w", err))
	}
	defer unlock()

	output, err := io.ReadAll(runOutputReader)
	if err != nil {
		return errorResult(fmt.Errorf("failed to read run output: %w", err))
	}

	return okResultWithOutput(string(output))
}
