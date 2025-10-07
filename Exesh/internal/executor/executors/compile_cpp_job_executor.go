package executors

import (
	"bytes"
	"context"
	"fmt"
	"log/slog"
	"time"

	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/jobs"
	"exesh/internal/domain/execution/results"
	"exesh/internal/runtime"
)

type CompileCppJobExecutor struct {
	log            *slog.Logger
	inputProvider  inputProvider
	outputProvider outputProvider
	runtime        runtime.Runtime
}

func NewCompileCppJobExecutor(log *slog.Logger, inputProvider inputProvider, outputProvider outputProvider, rt runtime.Runtime) *CompileCppJobExecutor {
	return &CompileCppJobExecutor{
		log:            log,
		inputProvider:  inputProvider,
		outputProvider: outputProvider,
		runtime:        rt,
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
	compileCppJob := job.(*jobs.CompileCppJob)

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

	stderr := bytes.NewBuffer(nil)
	err = e.runtime.Execute(ctx,
		[]string{"g++", "/main.cpp", "-o", "/a.out"},
		runtime.ExecuteParams{
			// TODO: Limits
			Limits:   runtime.Limits{},
			InFiles:  []runtime.File{{OutsideLocation: codeLocation, InsideLocation: "/main.cpp"}},
			OutFiles: []runtime.File{{OutsideLocation: compiledCodeLocation, InsideLocation: "/a.out"}},
			Stderr:   stderr,
		})
	if err != nil {
		e.log.Error("execute g++ in runtime error", slog.Any("err", err))
		return compilationErrorResult(stderr.String())
	}

	e.log.Info("command ok")
	return okResult()
}
