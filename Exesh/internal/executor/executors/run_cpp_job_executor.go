package executors

import (
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

type RunCppJobExecutor struct {
	log            *slog.Logger
	inputProvider  inputProvider
	outputProvider outputProvider
}

func NewRunCppJobExecutor(log *slog.Logger, inputProvider inputProvider, outputProvider outputProvider) *RunCppJobExecutor {
	return &RunCppJobExecutor{
		log:            log,
		inputProvider:  inputProvider,
		outputProvider: outputProvider,
	}
}

func (e *RunCppJobExecutor) SupportsType(jobType execution.JobType) bool {
	return jobType == execution.RunCppJobType
}

func (e *RunCppJobExecutor) Execute(ctx context.Context, job execution.Job) execution.Result {
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

	if job.GetType() != execution.RunCppJobType {
		return errorResult(fmt.Errorf("unsupported job type %s for %s executor", job.GetType(), execution.RunCppJobType))
	}
	runCppJob := job.(*jobs.RunCppJob)

	compiledCode, unlock, err := e.inputProvider.Locate(ctx, runCppJob.CompiledCode)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate compiled_code input: %w", err))
	}
	defer unlock()

	runInput, unlock, err := e.inputProvider.Read(ctx, runCppJob.RunInput)
	if err != nil {
		return errorResult(fmt.Errorf("failed to read run_input input: %w", err))
	}
	defer unlock()

	runOutput, commitOutput, abortOutput, err := e.outputProvider.Create(ctx, runCppJob.RunOutput)
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

	cmd := exec.CommandContext(ctx, "./"+compiledCode)

	cmd.Stdin = runInput
	cmd.Stdout = runOutput

	e.log.Info("do command", slog.Any("cmd", cmd))
	err = cmd.Run()
	if err != nil {
		var exitErr *exec.ExitError
		if !errors.As(err, &exitErr) {
			e.log.Error("command error", slog.Any("err", err))
			return errorResult(err)
		}
	}

	if commitErr := commit(); commitErr != nil {
		return errorResult(commitErr)
	}

	e.log.Info("command ok")

	if err != nil {
		return runtimeErrorResult()
	}

	if !runCppJob.ShowOutput {
		return okResult()
	}

	runOutputReader, unlock, err := e.outputProvider.Read(ctx, runCppJob.RunOutput)
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
