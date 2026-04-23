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

type RunPyJobExecutor struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	runtimeFactory runtime.RuntimeFactory
	runtime        runtime.Runtime

	job jobs.Job

	runtimeResourceRegistry *executor.RuntimeResourceRegistry
}

type RunPyExecutorFactory struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider

	runtimeFactory runtime.RuntimeFactory
}

func NewRunPyExecutorFactory(
	log *slog.Logger,
	sourceProvider sourceProvider,
	outputProvider outputProvider,
	runtimeFactory runtime.RuntimeFactory,
) *RunPyExecutorFactory {
	return &RunPyExecutorFactory{
		log:            log,
		sourceProvider: sourceProvider,
		outputProvider: outputProvider,

		runtimeFactory: runtimeFactory,
	}
}

func (f *RunPyExecutorFactory) SupportsType(jobType job.Type) bool {
	return jobType == job.RunPy
}

func (f *RunPyExecutorFactory) Create(jb jobs.Job) (executor.JobExecutor, error) {
	return f.CreateWithRuntime(jb, nil, executor.NewRuntimeResourceRegistry(8))
}

func (f *RunPyExecutorFactory) CreateWithRuntime(
	jb jobs.Job,
	rt runtime.Runtime,
	runtimeResourceRegistry *executor.RuntimeResourceRegistry,
) (executor.JobExecutor, error) {
	if jb.GetType() != job.RunPy {
		return nil, fmt.Errorf("unsupported job type %s for %s executor", jb.GetType(), job.RunPy)
	}
	if runtimeResourceRegistry == nil {
		runtimeResourceRegistry = executor.NewRuntimeResourceRegistry(8)
	}

	return &RunPyJobExecutor{
		log:                     f.log,
		sourceProvider:          f.sourceProvider,
		outputProvider:          f.outputProvider,
		runtimeFactory:          f.runtimeFactory,
		runtime:                 rt,
		runtimeResourceRegistry: runtimeResourceRegistry,

		job: jb,
	}, nil
}

func (e *RunPyJobExecutor) Init(ctx context.Context) error {
	if e.runtime == nil {
		rt, err := e.runtimeFactory.Create(ctx)
		if err != nil {
			return fmt.Errorf("failed to init runtime: %w", err)
		}
		e.runtime = rt
	}

	jb := e.job.AsRunPy()
	e.runtimeResourceRegistry.Set(jb.Code.SourceID, "solution.py")
	e.runtimeResourceRegistry.Set(jb.RunInput.SourceID, "input.txt")
	return nil
}

func (e *RunPyJobExecutor) PrepareInput(ctx context.Context) error {
	jb := e.job.AsRunPy()

	codePath, unlock, err := e.sourceProvider.Locate(ctx, jb.Code.SourceID)
	if err != nil {
		return fmt.Errorf("failed to get code: %w", err)
	}
	defer unlock()

	runInputPath, unlock, err := e.sourceProvider.Locate(ctx, jb.RunInput.SourceID)
	if err != nil {
		return fmt.Errorf("failed to get run input: %w", err)
	}
	defer unlock()

	codeRuntimePath, err := e.runtimeResourceRegistry.Get(jb.Code.SourceID)
	if err != nil {
		return fmt.Errorf("failed to get code runtime path: %w", err)
	}
	runInputRuntimePath, err := e.runtimeResourceRegistry.Get(jb.RunInput.SourceID)
	if err != nil {
		return fmt.Errorf("failed to get run input runtime path: %w", err)
	}

	if err = e.runtime.CopyToRuntime(ctx, codePath, codeRuntimePath); err != nil {
		return fmt.Errorf("failed to copy code to runtime: %w", err)
	}
	if err = e.runtime.CopyToRuntime(ctx, runInputPath, runInputRuntimePath); err != nil {
		return fmt.Errorf("failed to copy run input to runtime: %w", err)
	}

	return nil
}

func (e *RunPyJobExecutor) ExecuteCommand(ctx context.Context) results.Result {
	if e.runtimeResourceRegistry == nil {
		return results.Error(e.job, fmt.Errorf("runtime resource registry is not set"))
	}
	jb := e.job.AsRunPy()
	jobID := jb.GetID()

	e.log.Info("execute job", slog.String("job_id", jobID.String()))

	var (
		elapsedTime = 0
		usedMemory  = 0

		errorResult = func(err error) results.Result {
			return results.NewRunResultErr(jobID, err.Error(), elapsedTime, usedMemory)
		}
	)

	codeRuntimePath, err := e.runtimeResourceRegistry.Get(jb.Code.SourceID)
	if err != nil {
		return errorResult(fmt.Errorf("failed to get code runtime path: %w", err))
	}
	runInputRuntimePath, err := e.runtimeResourceRegistry.Get(jb.RunInput.SourceID)
	if err != nil {
		return errorResult(fmt.Errorf("failed to get run input runtime path: %w", err))
	}

	runOutputRuntimePath := "output.txt"
	stderr := bytes.NewBuffer(nil)
	usage, err := e.runtime.RunCommand(
		ctx,
		[]string{"/usr/bin/python3", codeRuntimePath},
		runtime.RunParams{
			Limits: runtime.Limits{
				Memory: runtime.MemoryLimit(int64(jb.MemoryLimit) * int64(runtime.Megabyte)),
				Time:   runtime.TimeLimit(int64(jb.TimeLimit) * int64(time.Millisecond)),
			},
			StdinFile:  runInputRuntimePath,
			StdoutFile: runOutputRuntimePath,
			Stderr:     stderr,
		},
	)

	if usage == nil {
		e.log.Error("execute python in runtime error", slog.Any("err", err))
		return errorResult(fmt.Errorf("execute binary in runtime error: %w", err))
	}

	elapsedTime = usage.ElapsedTime
	usedMemory = usage.UsedMemory

	e.log.Info("command ok")

	if err != nil {
		if errors.Is(err, runtime.ErrTimeout) {
			return results.NewRunResultTL(jobID, false, usage.ElapsedTime, usage.UsedMemory)
		}
		if errors.Is(err, runtime.ErrOutOfMemory) {
			return results.NewRunResultML(jobID, false, usage.ElapsedTime, usage.UsedMemory)
		}
		return results.NewRunResultRE(jobID, false, usage.ElapsedTime, usage.UsedMemory)
	}

	executor.RegisterJobOutputRuntimePath(e.runtimeResourceRegistry, jobID, runOutputRuntimePath)

	if !jb.ShowOutput {
		return results.NewRunResultOK(jobID, true, elapsedTime, usedMemory)
	}

	tmp, err := os.CreateTemp("/tmp", "*")
	if err != nil {
		return errorResult(fmt.Errorf("failed to create temporary run output file: %w", err))
	}
	defer func() { _ = os.Remove(tmp.Name()) }()
	defer func() { _ = tmp.Close() }()

	if err = e.runtime.CopyFromRuntime(ctx, runOutputRuntimePath, tmp.Name()); err != nil {
		return errorResult(fmt.Errorf("failed to copy run output from runtime: %w", err))
	}
	out, err := os.ReadFile(tmp.Name())
	if err != nil {
		return errorResult(fmt.Errorf("failed to read run output: %w", err))
	}
	return results.NewRunResultWithOutput(jobID, true, string(out), elapsedTime, usedMemory)
}

func (e *RunPyJobExecutor) SaveOutput(ctx context.Context) error {
	jb := e.job.AsRunPy()

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

	runOutputRuntimePath, err := executor.GetJobOutputRuntimePath(e.runtimeResourceRegistry, e.job.GetID())
	if err != nil {
		return fmt.Errorf("failed to get run_output runtimePath: %w", err)
	}
	if err = e.runtime.CopyFromRuntime(ctx, runOutputRuntimePath, runOutput); err != nil {
		return fmt.Errorf("failed to copy run_output from runtime: %w", err)
	}

	if commitErr := commit(); commitErr != nil {
		return commitErr
	}

	return nil
}

func (e *RunPyJobExecutor) Stop(ctx context.Context) error {
	if e.runtime == nil {
		return nil
	}
	return e.runtime.Stop(ctx)
}
