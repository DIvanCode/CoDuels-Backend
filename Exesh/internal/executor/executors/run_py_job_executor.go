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

type RunPyJobExecutor struct {
	log            *slog.Logger
	inputProvider  inputProvider
	outputProvider outputProvider
	runtime        runtime.Runtime
}

func NewRunPyJobExecutor(log *slog.Logger, inputProvider inputProvider, outputProvider outputProvider, rt runtime.Runtime) *RunPyJobExecutor {
	return &RunPyJobExecutor{
		log:            log,
		inputProvider:  inputProvider,
		outputProvider: outputProvider,
		runtime:        rt,
	}
}

func (e *RunPyJobExecutor) SupportsType(jobType execution.JobType) bool {
	return jobType == execution.RunPyJobType
}

func (e *RunPyJobExecutor) Execute(ctx context.Context, job execution.Job) execution.Result {
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

	if job.GetType() != execution.RunPyJobType {
		return errorResult(fmt.Errorf("unsupported job type %s for %s executor", job.GetType(), execution.RunPyJobType))
	}
	runPyJob := job.(*jobs.RunPyJob)

	code, unlock, err := e.inputProvider.Locate(ctx, runPyJob.Code)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate code input: %w", err))
	}
	defer unlock()

	runInput, unlock, err := e.inputProvider.Read(ctx, runPyJob.RunInput)
	if err != nil {
		return errorResult(fmt.Errorf("failed to read run_input input: %w", err))
	}
	defer unlock()

	runOutput, commitOutput, abortOutput, err := e.outputProvider.Create(ctx, runPyJob.RunOutput)
	if err != nil {
		return errorResult(fmt.Errorf("failed to create run_output output: %w", err))
	}
	commit := func() error {
		if err = commitOutput(); err != nil {
			_ = abortOutput()
			return fmt.Errorf("failed to commit run_output output: %w", err)
		}
		abortOutput = func() error { return nil }
		return nil
	}
	defer func() {
		_ = abortOutput()
	}()

	const codeLocation = "/main.py"

	stderr := bytes.NewBuffer(nil)
	err = e.runtime.Execute(ctx,
		[]string{"python3", codeLocation},
		runtime.ExecuteParams{
			// TODO: Limits
			Limits: runtime.Limits{
				Memory: runtime.MemoryLimit(int64(runPyJob.MemoryLimit) * int64(runtime.Megabyte)),
				Time:   runtime.TimeLimit(int64(runPyJob.TimeLimit) * int64(time.Millisecond)),
			},
			InFiles: []runtime.File{{OutsideLocation: code, InsideLocation: codeLocation}},
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

	if commitErr := commit(); commitErr != nil {
		return errorResult(commitErr)
	}

	if !runPyJob.ShowOutput {
		return okResult()
	}

	runOutputReader, unlock, err := e.outputProvider.Read(ctx, runPyJob.RunOutput)
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
