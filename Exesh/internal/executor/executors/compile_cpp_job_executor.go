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

type CompileCppJobExecutor struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	runtime        runtime.Runtime
}

func NewCompileCppJobExecutor(log *slog.Logger, sourceProvider sourceProvider, outputProvider outputProvider, rt runtime.Runtime) *CompileCppJobExecutor {
	return &CompileCppJobExecutor{
		log:            log,
		sourceProvider: sourceProvider,
		outputProvider: outputProvider,
		runtime:        rt,
	}
}

func (e *CompileCppJobExecutor) SupportsType(jobType job.Type) bool {
	return jobType == job.CompileCpp
}

func (e *CompileCppJobExecutor) Execute(ctx context.Context, jb jobs.Job) results.Result {
	errorResult := func(err error) results.Result {
		return results.NewCompileResultErr(jb.GetID(), err.Error())
	}

	if jb.GetType() != job.CompileCpp {
		return errorResult(fmt.Errorf("unsupported job type %s for %s executor", jb.GetType(), job.CompileCpp))
	}
	compileCppJob := jb.AsCompileCpp()

	code, unlock, err := e.sourceProvider.Locate(ctx, compileCppJob.Code.SourceID)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate code input: %w", err))
	}
	defer unlock()

	compiledCode, commitOutput, abortOutput, err := e.outputProvider.Reserve(ctx, jb.GetID(), compileCppJob.CompiledCode.File)
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

	const codeMountPath = "/main.cpp"
	const compiledCodeMountPath = "/a.out"

	stderr := bytes.NewBuffer(nil)
	err = e.runtime.Execute(ctx,
		[]string{"g++", codeMountPath, "-o", compiledCodeMountPath},
		runtime.ExecuteParams{
			// TODO: Limits
			Limits:   runtime.Limits{},
			InFiles:  []runtime.File{{OutsideLocation: code, InsideLocation: codeMountPath}},
			OutFiles: []runtime.File{{OutsideLocation: compiledCode, InsideLocation: compiledCodeMountPath}},
			Stderr:   stderr,
		})
	if err != nil {
		e.log.Error("execute g++ in runtime error", slog.Any("err", err))
		return results.NewCompileResultCE(jb.GetID(), stderr.String())
	}

	e.log.Info("command ok")

	if commitErr := commit(); commitErr != nil {
		return errorResult(commitErr)
	}

	return results.NewCompileResultOK(jb.GetID())
}
