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

type RunCppJobExecutor struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	runtimeFactory runtime.RuntimeFactory
	runtime        runtime.Runtime

	job jobs.Job
}

type RunCppExecutorFactory struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider

	runtimeFactory runtime.RuntimeFactory
}

func NewRunCppExecutorFactory(
	log *slog.Logger,
	sourceProvider sourceProvider,
	outputProvider outputProvider,
	runtimeFactory runtime.RuntimeFactory,
) *RunCppExecutorFactory {
	return &RunCppExecutorFactory{
		log:            log,
		sourceProvider: sourceProvider,
		outputProvider: outputProvider,

		runtimeFactory: runtimeFactory,
	}
}

func (f *RunCppExecutorFactory) SupportsType(jobType job.Type) bool {
	return jobType == job.RunCpp
}

func (f *RunCppExecutorFactory) Create(jb jobs.Job) (executor.JobExecutor, error) {
	if jb.GetType() != job.RunCpp {
		return nil, fmt.Errorf("unsupported job type %s for %s executor", jb.GetType(), job.RunCpp)
	}
	return &RunCppJobExecutor{
		log:            f.log,
		sourceProvider: f.sourceProvider,
		outputProvider: f.outputProvider,
		runtimeFactory: f.runtimeFactory,
		job:            jb,
	}, nil
}

func (e *RunCppJobExecutor) Init(ctx context.Context) error {
	rt, err := e.runtimeFactory.Create(ctx)
	if err != nil {
		return fmt.Errorf("failed to init runtime: %w", err)
	}
	e.runtime = rt
	return nil
}

func (e *RunCppJobExecutor) PrepareInput(ctx context.Context) error {
	jb := e.job.AsRunCpp()

	if err := copyInputToRuntime(ctx, e.sourceProvider, e.runtime, jb.CompiledCode.SourceID, "a.out"); err != nil {
		return fmt.Errorf("prepare compiled code: %w", err)
	}

	if err := copyInputToRuntime(ctx, e.sourceProvider, e.runtime, jb.RunInput.SourceID, "input.txt"); err != nil {
		return fmt.Errorf("prepare run input: %w", err)
	}

	return nil
}

func (e *RunCppJobExecutor) ExecuteCommand(ctx context.Context) results.Result {
	jb := e.job.AsRunCpp()
	jobID := jb.GetID()

	e.log.Info("execute job", slog.String("job_id", jobID.String()))

	var (
		elapsedTime = 0
		usedMemory  = 0

		errorResult = func(err error) results.Result {
			return results.NewRunResultErr(jobID, err.Error(), elapsedTime, usedMemory)
		}
	)

	compiledCodeRuntimePath := "a.out"
	runInputRuntimePath := "input.txt"

	runOutputRuntimePath := "output.txt"
	stderr := bytes.NewBuffer(nil)
	usage, err := e.runtime.RunCommand(
		ctx,
		[]string{"./" + compiledCodeRuntimePath},
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
		e.log.Error("execute binary in runtime error", slog.Any("err", err))
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

func (e *RunCppJobExecutor) SaveOutput(ctx context.Context, res *results.Result) error {
	jb := e.job.AsRunCpp()

	runOutput, commitOutput, abortOutput, err := e.outputProvider.Reserve(ctx, jb.GetID(), jb.RunOutput.File)
	if err != nil {
		trashTime, _ := abortOutput()
		res.SetArtifactTrashTime(trashTime)
		if errors.Is(err, errs.ErrFileAlreadyExists) {
			return nil
		}
		return fmt.Errorf("failed to reserve run_output output: %w", err)
	}
	commit := func() error {
		trashTime, commitErr := commitOutput()
		if commitErr != nil {
			_, _ = abortOutput()
			return fmt.Errorf("failed to commit run_output output: %w", err)
		}
		res.SetArtifactTrashTime(trashTime)
		abortOutput = func() (*time.Time, error) { return nil, nil }
		return nil
	}
	defer func() {
		_, _ = abortOutput()
	}()

	runOutputRuntimePath := "output.txt"
	if err = e.runtime.CopyFromRuntime(ctx, runOutputRuntimePath, runOutput); err != nil {
		return fmt.Errorf("failed to copy run_output from runtime: %w", err)
	}

	if commitErr := commit(); commitErr != nil {
		return commitErr
	}

	return nil
}

func (e *RunCppJobExecutor) Stop(ctx context.Context) error {
	if e.runtime == nil {
		return nil
	}
	return e.runtime.Stop(ctx)
}
