package executors

import (
	"bytes"
	"context"
	"errors"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/executor"
	"exesh/internal/runtime"
	"fmt"
	errs "github.com/DIvanCode/filestorage/pkg/errors"
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
	if jb.GetType() != job.CompileGo {
		return nil, fmt.Errorf("unsupported job type %s for %s executor", jb.GetType(), job.CompileGo)
	}
	return &CompileGoJobExecutor{
		log:            f.log,
		sourceProvider: f.sourceProvider,
		outputProvider: f.outputProvider,
		runtimeFactory: f.runtimeFactory,
		job:            jb,
	}, nil
}

func (e *CompileGoJobExecutor) Init(ctx context.Context) error {
	rt, err := e.runtimeFactory.Create(ctx)
	if err != nil {
		return fmt.Errorf("failed to init runtime: %w", err)
	}
	e.runtime = rt
	return nil
}

func (e *CompileGoJobExecutor) PrepareInput(ctx context.Context) error {
	jb := e.job.AsCompileGo()

	if err := copyInputToRuntime(ctx, e.sourceProvider, e.runtime, jb.Code.SourceID, "source.go"); err != nil {
		return fmt.Errorf("prepare code: %w", err)
	}

	return nil
}

func (e *CompileGoJobExecutor) ExecuteCommand(ctx context.Context) results.Result {
	jb := e.job.AsCompileGo()
	jobID := jb.GetID()

	e.log.Info("execute job", slog.String("job_id", jobID.String()))

	elapsedTime, usedMemory := 0, 0

	codeRuntimePath := "source.go"

	stderr := bytes.NewBuffer(nil)
	compiledCodeRuntimePath := "bin"
	usage, err := e.runtime.RunCommand(
		ctx,
		[]string{"/usr/local/go/bin/go", "build", "-o", compiledCodeRuntimePath, codeRuntimePath},
		runtime.RunParams{
			Limits: runtime.Limits{
				Memory: runtime.MemoryLimit(int64(jb.MemoryLimit) * int64(runtime.Megabyte)),
				Time:   runtime.TimeLimit(int64(jb.TimeLimit) * int64(time.Millisecond)),
			},
			Stderr: stderr,
		},
	)
	if err != nil {
		e.log.Error("execute go build in runtime error", slog.Any("err", err))
		return results.NewCompileResultCE(jobID, false, stderr.String(), usage.ElapsedTime, usage.UsedMemory)
	}

	elapsedTime = usage.ElapsedTime
	usedMemory = usage.UsedMemory

	e.log.Info("command ok")

	return results.NewCompileResultOK(jobID, true, elapsedTime, usedMemory)
}

func (e *CompileGoJobExecutor) SaveOutput(ctx context.Context, res *results.Result) error {
	jb := e.job.AsCompileGo()

	compiledCode, commitOutput, abortOutput, err := e.outputProvider.Reserve(ctx, jb.GetID(), jb.CompiledCode.File)
	if err != nil {
		trashTime, _ := abortOutput()
		res.SetArtifactTrashTime(trashTime)
		if errors.Is(err, errs.ErrFileAlreadyExists) {
			return nil
		}
		return fmt.Errorf("failed to reserve compiled_code output: %w", err)
	}
	commit := func() error {
		trashTime, commitErr := commitOutput()
		if commitErr != nil {
			_, _ = abortOutput()
			return fmt.Errorf("failed to commit compiled_code output: %w", err)
		}
		res.SetArtifactTrashTime(trashTime)
		abortOutput = func() (*time.Time, error) { return nil, nil }
		return nil
	}
	defer func() {
		_, _ = abortOutput()
	}()

	compiledCodeRuntimePath := "bin"
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
