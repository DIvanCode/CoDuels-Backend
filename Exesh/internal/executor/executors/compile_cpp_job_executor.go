package executors

import (
	"bytes"
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/executor"
	"exesh/internal/runtime"
	"fmt"
	"log/slog"
	"time"
)

type CompileCppJobExecutor struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	runtimeFactory runtime.RuntimeFactory
	runtime        runtime.Runtime

	job jobs.Job

	codeRuntimePath         string
	compiledCodeRuntimePath string
	runtimeResourceRegistry *executor.RuntimeResourceRegistry
}

type CompileCppExecutorFactory struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider

	runtimeFactory runtime.RuntimeFactory
}

func NewCompileCppExecutorFactory(
	log *slog.Logger,
	sourceProvider sourceProvider,
	outputProvider outputProvider,
	runtimeFactory runtime.RuntimeFactory,
) *CompileCppExecutorFactory {
	return &CompileCppExecutorFactory{
		log:            log,
		sourceProvider: sourceProvider,
		outputProvider: outputProvider,

		runtimeFactory: runtimeFactory,
	}
}

func (f *CompileCppExecutorFactory) SupportsType(jobType job.Type) bool {
	return jobType == job.CompileCpp
}

func (f *CompileCppExecutorFactory) Create(jb jobs.Job) (executor.JobExecutor, error) {
	return f.CreateWithRuntime(jb, nil, executor.NewRuntimeResourceRegistry(8))
}

func (f *CompileCppExecutorFactory) CreateWithRuntime(
	jb jobs.Job,
	rt runtime.Runtime,
	runtimeResourceRegistry *executor.RuntimeResourceRegistry,
) (executor.JobExecutor, error) {
	if jb.GetType() != job.CompileCpp {
		return nil, fmt.Errorf("unsupported job type %s for %s executor", jb.GetType(), job.CompileCpp)
	}
	if runtimeResourceRegistry == nil {
		runtimeResourceRegistry = executor.NewRuntimeResourceRegistry(8)
	}

	return &CompileCppJobExecutor{
		log:                     f.log,
		sourceProvider:          f.sourceProvider,
		outputProvider:          f.outputProvider,
		runtimeFactory:          f.runtimeFactory,
		runtime:                 rt,
		runtimeResourceRegistry: runtimeResourceRegistry,

		job: jb,
	}, nil
}

func (e *CompileCppJobExecutor) Init(ctx context.Context) error {
	if e.runtime == nil {
		rt, err := e.runtimeFactory.Create(ctx)
		if err != nil {
			return fmt.Errorf("failed to init runtime: %w", err)
		}
		e.runtime = rt
	}
	return nil
}

func (e *CompileCppJobExecutor) PrepareInput(ctx context.Context) error {
	jb := e.job.AsCompileCpp()

	codePath, unlock, err := e.sourceProvider.Locate(ctx, jb.Code.SourceID)
	if err != nil {
		return fmt.Errorf("failed to get code: %w", err)
	}
	defer unlock()

	e.codeRuntimePath = "source.cpp"
	if err = e.runtime.CopyToRuntime(ctx, codePath, e.codeRuntimePath); err != nil {
		return fmt.Errorf("failed to copy code to runtime: %w", err)
	}
	e.runtimeResourceRegistry.Set(jb.Code.SourceID, e.codeRuntimePath)

	return nil
}

func (e *CompileCppJobExecutor) ExecuteCommand(ctx context.Context) results.Result {
	if e.runtimeResourceRegistry == nil {
		return results.Error(e.job, fmt.Errorf("runtime resource registry is not set"))
	}

	jb := e.job.AsCompileCpp()
	codeRuntimePath, err := e.runtimeResourceRegistry.Get(jb.Code.SourceID)
	if err != nil {
		return results.Error(e.job, err)
	}
	e.codeRuntimePath = codeRuntimePath

	stderr := bytes.NewBuffer(nil)
	e.compiledCodeRuntimePath = "a.out"
	err = e.runtime.RunCommand(
		ctx,
		[]string{"g++", e.codeRuntimePath, "-o", e.compiledCodeRuntimePath},
		runtime.RunParams{
			Limits: runtime.Limits{
				Memory: runtime.MemoryLimit(1024 * int64(runtime.Megabyte)),
				Time:   runtime.TimeLimit(15000 * int64(time.Millisecond)),
			},
			Stderr: stderr,
		},
	)
	if err != nil {
		e.log.Error("execute g++ in runtime error", slog.Any("err", err))
		return results.NewCompileResultCE(jb.GetID(), stderr.String())
	}

	e.log.Info("command ok")
	executor.RegisterJobOutputRuntimePath(e.runtimeResourceRegistry, jb.GetID(), e.compiledCodeRuntimePath)

	return results.NewCompileResultOK(jb.GetID())
}

func (e *CompileCppJobExecutor) SaveOutput(ctx context.Context) error {
	jb := e.job.AsCompileCpp()

	compiledCode, commitOutput, abortOutput, err := e.outputProvider.Reserve(ctx, jb.GetID(), jb.CompiledCode.File)
	if err != nil {
		return fmt.Errorf("failed to reserve compiled_code output: %w", err)
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

	if err = e.runtime.CopyFromRuntime(ctx, e.compiledCodeRuntimePath, compiledCode); err != nil {
		return fmt.Errorf("failed to copy compiled_code from runtime: %w", err)
	}

	if commitErr := commit(); commitErr != nil {
		return commitErr
	}

	return nil
}

func (e *CompileCppJobExecutor) Stop(ctx context.Context) error {
	if e.runtime == nil {
		return nil
	}
	return e.runtime.Stop(ctx)
}
