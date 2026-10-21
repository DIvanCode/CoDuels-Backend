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
	"strings"
	"time"
)

type CheckCppJobExecutor struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	runtimeFactory runtime.RuntimeFactory
	runtime        runtime.Runtime

	job jobs.Job
}

type CheckCppExecutorFactory struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider

	runtimeFactory runtime.RuntimeFactory
}

func NewCheckCppExecutorFactory(
	log *slog.Logger,
	sourceProvider sourceProvider,
	outputProvider outputProvider,
	runtimeFactory runtime.RuntimeFactory,
) *CheckCppExecutorFactory {
	return &CheckCppExecutorFactory{
		log:            log,
		sourceProvider: sourceProvider,
		outputProvider: outputProvider,

		runtimeFactory: runtimeFactory,
	}
}

func (f *CheckCppExecutorFactory) SupportsType(jobType job.Type) bool {
	return jobType == job.CheckCpp
}

func (f *CheckCppExecutorFactory) Create(jb jobs.Job) (executor.JobExecutor, error) {
	if jb.GetType() != job.CheckCpp {
		return nil, fmt.Errorf("unsupported job type %s for %s executor", jb.GetType(), job.CheckCpp)
	}
	return &CheckCppJobExecutor{
		log:            f.log,
		sourceProvider: f.sourceProvider,
		outputProvider: f.outputProvider,
		runtimeFactory: f.runtimeFactory,
		job:            jb,
	}, nil
}

func (e *CheckCppJobExecutor) Init(ctx context.Context) error {
	rt, err := e.runtimeFactory.Create(ctx)
	if err != nil {
		return fmt.Errorf("failed to init runtime: %w", err)
	}
	e.runtime = rt
	return nil
}

func (e *CheckCppJobExecutor) PrepareInput(ctx context.Context) error {
	jb := e.job.AsCheckCpp()

	if err := copyInputToRuntime(ctx, e.sourceProvider, e.runtime, jb.CompiledChecker.SourceID, "checker"); err != nil {
		return fmt.Errorf("prepare compiled checker: %w", err)
	}

	if err := copyInputToRuntime(ctx, e.sourceProvider, e.runtime, jb.TestInput.SourceID, "input.txt"); err != nil {
		return fmt.Errorf("prepare test input: %w", err)
	}

	if err := copyInputToRuntime(ctx, e.sourceProvider, e.runtime, jb.CorrectOutput.SourceID, "correct.txt"); err != nil {
		return fmt.Errorf("prepare correct output: %w", err)
	}

	if err := copyInputToRuntime(ctx, e.sourceProvider, e.runtime, jb.SuspectOutput.SourceID, "suspect.txt"); err != nil {
		return fmt.Errorf("prepare suspect output: %w", err)
	}

	return nil
}

func (e *CheckCppJobExecutor) ExecuteCommand(ctx context.Context) results.Result {
	jb := e.job.AsCheckCpp()
	jobID := jb.GetID()

	e.log.Info("execute job", slog.String("job_id", jobID.String()))

	var (
		elapsedTime = 0
		usedMemory  = 0

		errorResult = func(err error) results.Result {
			return results.NewCheckResultErr(jb.GetID(), err.Error(), elapsedTime, usedMemory)
		}
	)

	checkerRuntimePath := "checker"
	testInputRuntimePath := "input.txt"
	correctRuntimePath := "correct.txt"
	suspectRuntimePath := "suspect.txt"

	stderr := bytes.NewBuffer(nil)
	usage, err := e.runtime.RunCommand(
		ctx,
		[]string{"./" + checkerRuntimePath, testInputRuntimePath, suspectRuntimePath, correctRuntimePath},
		runtime.RunParams{
			Limits: runtime.Limits{
				Memory: runtime.MemoryLimit(int64(jb.MemoryLimit) * int64(runtime.Megabyte)),
				Time:   runtime.TimeLimit(int64(jb.TimeLimit) * int64(time.Millisecond)),
			},
			Stderr: stderr,
		},
	)

	if usage == nil {
		e.log.Error("execute checker in runtime error", slog.Any("err", err))
		return errorResult(fmt.Errorf("failed to execute checker: %v", err))
	}

	elapsedTime = usage.ElapsedTime
	usedMemory = usage.UsedMemory
	verdict := strings.TrimSpace(stderr.String())

	if strings.HasPrefix(verdict, "ok") {
		e.log.Info("command ok")
		return results.NewCheckResultOK(jb.GetID(), false, usage.ElapsedTime, usage.UsedMemory)
	}
	if strings.HasPrefix(verdict, "wrong") {
		e.log.Info("command ok")
		return results.NewCheckResultWA(jb.GetID(), false, usage.ElapsedTime, usage.UsedMemory)
	}
	if err == nil {
		e.log.Info("failed to parse check verdict")
		return errorResult(fmt.Errorf("failed to parse check verdict: %s", verdict))
	}

	e.log.Error("execute checker in runtime error", slog.Any("err", err))
	return errorResult(fmt.Errorf("failed to execute checker: %v", err))
}

func (e *CheckCppJobExecutor) SaveOutput(_ context.Context, _ *results.Result) error {
	return nil
}

func (e *CheckCppJobExecutor) Stop(ctx context.Context) error {
	if e.runtime == nil {
		return nil
	}
	return e.runtime.Stop(ctx)
}
