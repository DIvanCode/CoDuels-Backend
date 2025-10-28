package executors

import (
	"bytes"
	"context"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/jobs"
	"exesh/internal/domain/execution/results"
	"exesh/internal/runtime"
	"fmt"
	"log/slog"
	"time"
)

type CompileGoJobExecutor struct {
	log            *slog.Logger
	inputProvider  inputProvider
	outputProvider outputProvider
	runtime        runtime.Runtime
}

func NewCompileGoJobExecutor(log *slog.Logger, inputProvider inputProvider, outputProvider outputProvider, rt runtime.Runtime) *CompileGoJobExecutor {
	return &CompileGoJobExecutor{
		log:            log,
		inputProvider:  inputProvider,
		outputProvider: outputProvider,
		runtime:        rt,
	}
}

func (e *CompileGoJobExecutor) SupportsType(jobType execution.JobType) bool {
	return jobType == execution.CompileGoJobType
}

func (e *CompileGoJobExecutor) Execute(ctx context.Context, job execution.Job) execution.Result {
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

	if job.GetType() != execution.CompileGoJobType {
		return errorResult(fmt.Errorf("unsupported job type %s for %s executor", job.GetType(), execution.CompileGoJobType))
	}
	compileGoJob := job.(*jobs.CompileGoJob)

	code, unlock, err := e.inputProvider.Locate(ctx, compileGoJob.Code)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate code input: %w", err))
	}
	defer unlock()

	compiledCode, commitOutput, abortOutput, err := e.outputProvider.Reserve(ctx, compileGoJob.CompiledCode)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate compiled_code output: %w", err))
	}
	commit := func() error {
		if err = commitOutput(); err != nil {
			_ = abortOutput()
			return fmt.Errorf("failed to commit compiled_code output: %w", err)
		}
		abortOutput = func() error { return nil }
		return nil
	}
	defer func() {
		_ = abortOutput()
	}()

	stderr := bytes.NewBuffer(nil)
	err = e.runtime.Execute(ctx,
		[]string{"go", "build", "-o", "/a.out", "/main.go"},
		runtime.ExecuteParams{
			// TODO: Limits
			Limits:   runtime.Limits{},
			InFiles:  []runtime.File{{OutsideLocation: code, InsideLocation: "/main.go"}},
			OutFiles: []runtime.File{{OutsideLocation: compiledCode, InsideLocation: "/a.out"}},
			Stderr:   stderr,
		})
	if err != nil {
		e.log.Error("execute go build in runtime error", slog.Any("err", err))
		return compilationErrorResult(stderr.String())
	}

	e.log.Info("command ok")

	if commitErr := commit(); commitErr != nil {
		return errorResult(commitErr)
	}

	return okResult()
}
