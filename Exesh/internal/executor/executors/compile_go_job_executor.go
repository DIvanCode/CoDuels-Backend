package executors

import (
	"bytes"
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/runtime"
	"fmt"
	"log/slog"
)

type CompileGoJobExecutor struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	runtime        runtime.Runtime
}

func NewCompileGoJobExecutor(log *slog.Logger, sourceProvider sourceProvider, outputProvider outputProvider, rt runtime.Runtime) *CompileGoJobExecutor {
	return &CompileGoJobExecutor{
		log:            log,
		sourceProvider: sourceProvider,
		outputProvider: outputProvider,
		runtime:        rt,
	}
}

func (e *CompileGoJobExecutor) SupportsType(jobType job.Type) bool {
	return jobType == job.CompileGo
}

func (e *CompileGoJobExecutor) Execute(ctx context.Context, jb jobs.Job) results.Result {
	errorResult := func(err error) results.Result {
		return results.NewCompileResultErr(jb.GetID(), err.Error())
	}
	if jb.GetType() != job.CompileGo {
		return errorResult(fmt.Errorf("unsupported job type %s for %s executor", jb.GetType(), job.CompileGo))
	}
	compileGoJob := jb.AsCompileGo()

	code, unlock, err := e.sourceProvider.Locate(ctx, compileGoJob.Code.SourceID)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate code input: %w", err))
	}
	defer unlock()

	compiledCode, commitOutput, abortOutput, err := e.outputProvider.Reserve(ctx, jb.GetID(), compileGoJob.CompiledCode.File)
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

	const codeMountPath = "/main.go"
	const compiledCodeMountPath = "/a.out"

	stderr := bytes.NewBuffer(nil)
	err = e.runtime.Execute(ctx,
		[]string{"go", "build", "-o", compiledCodeMountPath, codeMountPath},
		runtime.ExecuteParams{
			// TODO: Limits
			Limits:   runtime.Limits{},
			InFiles:  []runtime.File{{OutsideLocation: code, InsideLocation: codeMountPath}},
			OutFiles: []runtime.File{{OutsideLocation: compiledCode, InsideLocation: compiledCodeMountPath}},
			Stderr:   stderr,
		})
	if err != nil {
		e.log.Error("execute go build in runtime error", slog.Any("err", err))
		return results.NewCompileResultErr(jb.GetID(), stderr.String())
	}

	e.log.Info("command ok")

	if commitErr := commit(); commitErr != nil {
		return errorResult(commitErr)
	}

	return results.NewCompileResultOK(jb.GetID())
}
