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

type CompileGoJobExecutor struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	runtimeFactory runtime.RuntimeFactory
	runtime        runtime.Runtime

	job jobs.Job

	runtimeResourceRegistry *executor.RuntimeResourceRegistry
}

type CompileGoExecutorFactory struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider

	runtimeFactory runtime.RuntimeFactory
}

func NewCompileGoExecutorFactory(
	log *slog.Logger,
	sourceProvider sourceProvider,
	outputProvider outputProvider,
	runtimeFactory runtime.RuntimeFactory,
) *CompileGoExecutorFactory {
	return &CompileGoExecutorFactory{
		log:            log,
		sourceProvider: sourceProvider,
		outputProvider: outputProvider,

		runtimeFactory: runtimeFactory,
	}
}

func (f *CompileGoExecutorFactory) SupportsType(jobType job.Type) bool {
	return jobType == job.CompileGo
}

func (f *CompileGoExecutorFactory) Create(jb jobs.Job) (executor.JobExecutor, error) {
	return f.CreateWithRuntime(jb, nil, executor.NewRuntimeResourceRegistry(8))
}

func (f *CompileGoExecutorFactory) CreateWithRuntime(
	jb jobs.Job,
	rt runtime.Runtime,
	runtimeResourceRegistry *executor.RuntimeResourceRegistry,
) (executor.JobExecutor, error) {
	if jb.GetType() != job.CompileGo {
		return nil, fmt.Errorf("unsupported job type %s for %s executor", jb.GetType(), job.CompileGo)
	}
	if runtimeResourceRegistry == nil {
		runtimeResourceRegistry = executor.NewRuntimeResourceRegistry(8)
	}

	return &CompileGoJobExecutor{
		log:                     f.log,
		sourceProvider:          f.sourceProvider,
		outputProvider:          f.outputProvider,
		runtimeFactory:          f.runtimeFactory,
		runtime:                 rt,
		runtimeResourceRegistry: runtimeResourceRegistry,

		job: jb,
	}, nil
}

func (e *CompileGoJobExecutor) Init(ctx context.Context) error {
	if e.runtime == nil {
		rt, err := e.runtimeFactory.Create(ctx)
		if err != nil {
			return fmt.Errorf("failed to init runtime: %w", err)
		}
		e.runtime = rt
	}

	jb := e.job.AsCompileGo()
	e.runtimeResourceRegistry.Set(jb.Code.SourceID, "source.go")
	return nil
}

func (e *CompileGoJobExecutor) PrepareInput(ctx context.Context) error {
	jb := e.job.AsCompileGo()

	codePath, unlock, err := e.sourceProvider.Locate(ctx, jb.Code.SourceID)
	if err != nil {
		return fmt.Errorf("failed to get code: %w", err)
	}
	defer unlock()

	codeRuntimePath, err := e.runtimeResourceRegistry.Get(jb.Code.SourceID)
	if err != nil {
		return fmt.Errorf("failed to get codeRuntimePath: %w", err)
	}

	if err = e.runtime.CopyToRuntime(ctx, codePath, codeRuntimePath); err != nil {
		return fmt.Errorf("failed to copy code to runtime: %w", err)
	}

	return nil
}

func (e *CompileGoJobExecutor) ExecuteCommand(ctx context.Context) results.Result {
	if e.runtimeResourceRegistry == nil {
		return results.Error(e.job, fmt.Errorf("runtime resource registry is not set"))
	}

	jb := e.job.AsCompileGo()
	codeRuntimePath, err := e.runtimeResourceRegistry.Get(jb.Code.SourceID)
	if err != nil {
		return results.Error(e.job, err)
	}

	stderr := bytes.NewBuffer(nil)
	compiledCodeRuntimePath := "bin"
	err = e.runtime.RunCommand(
		ctx,
		[]string{"/usr/local/go/bin/go", "build", "-o", compiledCodeRuntimePath, codeRuntimePath},
		runtime.RunParams{
			Limits: runtime.Limits{
				Memory: runtime.MemoryLimit(1024 * int64(runtime.Megabyte)),
				Time:   runtime.TimeLimit(15000 * int64(time.Millisecond)),
			},
			Stderr: stderr,
		},
	)
	if err != nil {
		e.log.Error("execute go build in runtime error", slog.Any("err", err))
		return results.NewCompileResultCE(jb.GetID(), stderr.String())
	}

	e.log.Info("command ok")
	executor.RegisterJobOutputRuntimePath(e.runtimeResourceRegistry, jb.GetID(), compiledCodeRuntimePath)

	return results.NewCompileResultOK(jb.GetID())
}

func (e *CompileGoJobExecutor) SaveOutput(ctx context.Context) error {
	jb := e.job.AsCompileGo()

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

	compiledCodeRuntimePath, err := executor.GetJobOutputRuntimePath(e.runtimeResourceRegistry, e.job.GetID())
	if err != nil {
		return fmt.Errorf("failed to get compiled_code runtimePath: %w", err)
	}
	if err = e.runtime.CopyFromRuntime(ctx, compiledCodeRuntimePath, compiledCode); err != nil {
		return fmt.Errorf("failed to copy compiled_code from runtime: %w", err)
	}

	if commitErr := commit(); commitErr != nil {
		return commitErr
	}

	return nil
}

func (e *CompileGoJobExecutor) Stop(ctx context.Context) error {
	if e.runtime == nil {
		return nil
	}
	return e.runtime.Stop(ctx)
}
