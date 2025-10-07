package executors

import (
	"bytes"
	"context"
	"errors"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/jobs"
	"exesh/internal/domain/execution/results"
	"fmt"
	"io"
	"log/slog"
	"os/exec"
	"time"
)

type RunGoJobExecutor struct {
	log            *slog.Logger
	inputProvider  inputProvider
	outputProvider outputProvider
}

func NewRunGoJobExecutor(log *slog.Logger, inputProvider inputProvider, outputProvider outputProvider) *RunGoJobExecutor {
	return &RunGoJobExecutor{
		log:            log,
		inputProvider:  inputProvider,
		outputProvider: outputProvider,
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

	if job.GetType() != execution.RunGoJobType {
		return errorResult(fmt.Errorf("unsupported job type %s for %s executor", job.GetType(), execution.RunGoJobType))
	}
	runGoJob := job.(*jobs.RunGoJob)

	var codeLocation string
	codeLocation, unlock, err := e.inputProvider.Locate(ctx, runGoJob.Code)
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

	cmd := exec.CommandContext(ctx, "go", "run", codeLocation)

	cmd.Stdin = runInput
	cmd.Stdout = runOutput

	stderr := bytes.Buffer{}
	cmd.Stderr = &stderr

	e.log.Info("do command", slog.Any("cmd", cmd))
	if err = cmd.Run(); err != nil {
		e.log.Info("command error", slog.Any("err", err))

		var exitErr *exec.ExitError
		if !errors.As(err, &exitErr) {
			_ = abort()
			return errorResult(err)
		}

		_ = abort()
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

	output, err := io.ReadAll(runOutputReader)
	if err != nil {
		return errorResult(fmt.Errorf("failed to read run output: %w", err))
	}

	return okResultWithOutput(string(output))
}
