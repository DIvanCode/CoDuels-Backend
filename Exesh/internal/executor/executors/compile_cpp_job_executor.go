package executors

import (
	"bytes"
	"context"
	"errors"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/jobs"
	"exesh/internal/domain/execution/results"
	"fmt"
	"log/slog"
	"os/exec"
	"time"
)

type CompileCppJobExecutor struct {
	log            *slog.Logger
	inputProvider  inputProvider
	outputProvider outputProvider
}

func NewCompileCppJobExecutor(log *slog.Logger, inputProvider inputProvider, outputProvider outputProvider) *CompileCppJobExecutor {
	return &CompileCppJobExecutor{
		log:            log,
		inputProvider:  inputProvider,
		outputProvider: outputProvider,
	}
}

func (e *CompileCppJobExecutor) SupportsType(jobType execution.JobType) bool {
	return jobType == execution.CompileCppJobType
}

func (e *CompileCppJobExecutor) Execute(ctx context.Context, job execution.Job) execution.Result {
	errorResult := func(err error) execution.Result {
		return results.CompileResult{
			ResultDetails: execution.ResultDetails{
				ID:     job.GetID(),
				Type:   execution.CompileResult,
				DoneAt: time.Now(),
				Error:  err.Error(),
			},
		}
	}

	compilationErrorResult := func(compilationError string) execution.Result {
		return results.CompileResult{
			ResultDetails: execution.ResultDetails{
				ID:     job.GetID(),
				Type:   execution.CompileResult,
				DoneAt: time.Now(),
			},
			Status:           results.CompileStatusCE,
			CompilationError: compilationError,
		}
	}

	okResult := func() execution.Result {
		return results.CompileResult{
			ResultDetails: execution.ResultDetails{
				ID:     job.GetID(),
				Type:   execution.CompileResult,
				DoneAt: time.Now(),
			},
			Status: results.CompileStatusOK,
		}
	}

	if job.GetType() != execution.CompileCppJobType {
		return errorResult(fmt.Errorf("unsupported job type %s for %s executor", job.GetType(), execution.CompileCppJobType))
	}
	compileCppJob := job.(jobs.CompileCppJob)

	var codeLocation string
	codeLocation, unlock, err := e.inputProvider.Locate(ctx, compileCppJob.Code)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate code input: %w", err))
	}
	defer unlock()

	_, commit, abort, err := e.outputProvider.Create(ctx, compileCppJob.CompiledCode)
	if err != nil {
		return errorResult(fmt.Errorf("failed to create compiled_code output: %w", err))
	}
	if err = commit(); err != nil {
		_ = abort()
		return errorResult(fmt.Errorf("failed to commit compiled_code output creation: %w", err))
	}

	var compiledCodeLocation string
	compiledCodeLocation, unlock, err = e.outputProvider.Locate(ctx, compileCppJob.CompiledCode)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate compiled_code output: %w", err))
	}
	defer unlock()

	cmd := exec.CommandContext(ctx, "g++", codeLocation, "-o", compiledCodeLocation)

	stderr := bytes.Buffer{}
	cmd.Stderr = &stderr

	e.log.Info("do command", slog.Any("cmd", cmd))
	if err = cmd.Run(); err != nil {
		e.log.Info("command error", slog.Any("err", err))

		var exitErr *exec.ExitError
		if !errors.As(err, &exitErr) {
			return errorResult(err)
		}

		return compilationErrorResult(stderr.String())
	}

	e.log.Info("command ok")
	return okResult()
}
