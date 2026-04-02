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
	runtime        runtime.Runtime
	runtimeID      runtime.ID

	job jobs.Job

	codeRuntimePath         string
	compiledCodeRuntimePath string
}

type CompileGoExecutorFactory struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	runtime        runtime.Runtime
}

func NewCompileGoExecutorFactory(log *slog.Logger, sourceProvider sourceProvider, outputProvider outputProvider, rt runtime.Runtime) *CompileGoExecutorFactory {
	return &CompileGoExecutorFactory{
		log:            log,
		sourceProvider: sourceProvider,
		outputProvider: outputProvider,
		runtime:        rt,
	}
}

func (f *CompileGoExecutorFactory) SupportsType(jobType job.Type) bool {
	return jobType == job.CompileGo
}

func (f *CompileGoExecutorFactory) Create(jb jobs.Job) (executor.JobExecutor, error) {
	if jb.GetType() != job.CompileGo {
		return nil, fmt.Errorf("unsupported job type %s for %s executor", jb.GetType(), job.CompileGo)
	}
	return &CompileGoJobExecutor{
		log:            f.log,
		sourceProvider: f.sourceProvider,
		outputProvider: f.outputProvider,
		runtime:        f.runtime,

		job: jb,
	}, nil
}

func (e *CompileGoJobExecutor) Init(ctx context.Context) error {
	runtimeID, err := e.runtime.Init(ctx)
	if err != nil {
		return fmt.Errorf("failed to init runtime: %w", err)
	}
	e.runtimeID = runtimeID
	return nil
}

func (e *CompileGoJobExecutor) PrepareInput(ctx context.Context) error {
	jb := e.job.AsCompileGo()

	code, unlock, err := e.sourceProvider.Locate(ctx, jb.Code.SourceID)
	if err != nil {
		return fmt.Errorf("failed to get code: %w", err)
	}
	defer unlock()

	e.codeRuntimePath = "source.go"
	if err = e.runtime.CopyToRuntime(ctx, e.runtimeID, code, e.codeRuntimePath); err != nil {
		return fmt.Errorf("failed to copy code to runtime: %w", err)
	}

	return nil
}

func (e *CompileGoJobExecutor) ExecuteCommand(ctx context.Context) results.Result {
	jb := e.job.AsCompileGo()

	stderr := bytes.NewBuffer(nil)
	e.compiledCodeRuntimePath = "bin"
	err := e.runtime.RunCommand(
		ctx,
		e.runtimeID,
		[]string{"go", "build", "-o", e.compiledCodeRuntimePath, e.codeRuntimePath},
		runtime.RunParams{
			Limits: runtime.Limits{
				Memory: runtime.MemoryLimit(1024 * int64(runtime.Megabyte)),
				Time:   runtime.TimeLimit(5000 * int64(time.Millisecond)),
			},
			Stderr: stderr,
		},
	)
	if err != nil {
		e.log.Error("execute go build in runtime error", slog.Any("err", err))
		return results.NewCompileResultCE(jb.GetID(), stderr.String())
	}

	e.log.Info("command ok")

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

	if err = e.runtime.CopyFromRuntime(ctx, e.runtimeID, e.compiledCodeRuntimePath, compiledCode); err != nil {
		return fmt.Errorf("failed to copy compiled_code from runtime: %w", err)
	}

	if commitErr := commit(); commitErr != nil {
		return commitErr
	}

	return nil
}

func (e *CompileGoJobExecutor) Stop(ctx context.Context) error {
	return e.runtime.Stop(ctx, e.runtimeID)
}
