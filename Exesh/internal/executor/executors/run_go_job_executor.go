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
	"os"
	"time"
)

type RunGoJobExecutor struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	runtime        runtime.Runtime
	runtimeID      runtime.ID

	job jobs.Job

	compiledCodeRuntimePath string
	runInputRuntimePath     string
	runOutputRuntimePath    string
}

type RunGoExecutorFactory struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	runtime        runtime.Runtime
}

func NewRunGoExecutorFactory(log *slog.Logger, sourceProvider sourceProvider, outputProvider outputProvider, rt runtime.Runtime) *RunGoExecutorFactory {
	return &RunGoExecutorFactory{
		log:            log,
		sourceProvider: sourceProvider,
		outputProvider: outputProvider,
		runtime:        rt,
	}
}

func (f *RunGoExecutorFactory) SupportsType(jobType job.Type) bool {
	return jobType == job.RunGo
}

func (f *RunGoExecutorFactory) Create(jb jobs.Job) (executor.JobExecutor, error) {
	if jb.GetType() != job.RunGo {
		return nil, fmt.Errorf("unsupported job type %s for %s executor", jb.GetType(), job.RunGo)
	}
	return &RunGoJobExecutor{
		log:            f.log,
		sourceProvider: f.sourceProvider,
		outputProvider: f.outputProvider,
		runtime:        f.runtime,

		job: jb,
	}, nil
}

func (e *RunGoJobExecutor) Init(ctx context.Context) error {
	runtimeID, err := e.runtime.Init(ctx)
	if err != nil {
		return fmt.Errorf("failed to init runtime: %w", err)
	}
	e.runtimeID = runtimeID
	return nil
}

func (e *RunGoJobExecutor) PrepareInput(ctx context.Context) error {
	jb := e.job.AsRunGo()

	compiledCode, unlock, err := e.sourceProvider.Locate(ctx, jb.CompiledCode.SourceID)
	if err != nil {
		return fmt.Errorf("failed to get compiled code: %w", err)
	}
	defer unlock()

	runInput, unlock, err := e.sourceProvider.Locate(ctx, jb.RunInput.SourceID)
	if err != nil {
		return fmt.Errorf("failed to get run input: %w", err)
	}
	defer unlock()

	e.compiledCodeRuntimePath = "compiled"
	e.runInputRuntimePath = "input.txt"

	if err = e.runtime.CopyToRuntime(ctx, e.runtimeID, compiledCode, e.compiledCodeRuntimePath); err != nil {
		return fmt.Errorf("failed to copy compiled code to runtime: %w", err)
	}
	if err = e.runtime.CopyToRuntime(ctx, e.runtimeID, runInput, e.runInputRuntimePath); err != nil {
		return fmt.Errorf("failed to copy run input to runtime: %w", err)
	}

	return nil
}

func (e *RunGoJobExecutor) ExecuteCommand(ctx context.Context) results.Result {
	jb := e.job.AsRunGo()

	e.runOutputRuntimePath = "output.txt"
	stderr := bytes.NewBuffer(nil)
	err := e.runtime.RunCommand(
		ctx,
		e.runtimeID,
		[]string{"./" + e.compiledCodeRuntimePath},
		runtime.RunParams{
			Limits: runtime.Limits{
				Memory: runtime.MemoryLimit(int64(jb.MemoryLimit) * int64(runtime.Megabyte)),
				Time:   runtime.TimeLimit(int64(jb.TimeLimit) * int64(time.Millisecond)),
			},
			StdinFile:  e.runInputRuntimePath,
			StdoutFile: e.runOutputRuntimePath,
			Stderr:     stderr,
		},
	)
	if err != nil {
		e.log.Error("execute binary in runtime error", slog.Any("err", err))
		if errors.Is(err, runtime.ErrTimeout) {
			return results.NewRunResultTL(jb.GetID())
		}
		if errors.Is(err, runtime.ErrOutOfMemory) {
			return results.NewRunResultML(jb.GetID())
		}
		return results.NewRunResultRE(jb.GetID())
	}

	e.log.Info("command ok")

	if !jb.ShowOutput {
		return results.NewRunResultOK(jb.GetID())
	}

	tmp, err := os.CreateTemp("/tmp", "*")
	if err != nil {
		return results.Error(e.job, fmt.Errorf("failed to create temporary run output file: %w", err))
	}
	defer func() { _ = os.Remove(tmp.Name()) }()
	defer func() { _ = tmp.Close() }()

	if err = e.runtime.CopyFromRuntime(ctx, e.runtimeID, e.runOutputRuntimePath, tmp.Name()); err != nil {
		return results.Error(e.job, fmt.Errorf("failed to copy run output from runtime: %w", err))
	}
	out, err := os.ReadFile(tmp.Name())
	if err != nil {
		return results.Error(e.job, fmt.Errorf("failed to read run output: %w", err))
	}
	return results.NewRunResultWithOutput(jb.GetID(), string(out))
}

func (e *RunGoJobExecutor) SaveOutput(ctx context.Context) error {
	jb := e.job.AsRunGo()

	runOutput, commitOutput, abortOutput, err := e.outputProvider.Reserve(ctx, jb.GetID(), jb.RunOutput.File)
	if err != nil {
		if errors.Is(err, errs.ErrFileAlreadyExists) {
			return nil
		}
		return fmt.Errorf("failed to reserve run_output output: %w", err)
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

	if err = e.runtime.CopyFromRuntime(ctx, e.runtimeID, e.runOutputRuntimePath, runOutput); err != nil {
		return fmt.Errorf("failed to copy run_output from runtime: %w", err)
	}

	if commitErr := commit(); commitErr != nil {
		return commitErr
	}

	return nil
}

func (e *RunGoJobExecutor) Stop(ctx context.Context) error {
	return e.runtime.Stop(ctx, e.runtimeID)
}
