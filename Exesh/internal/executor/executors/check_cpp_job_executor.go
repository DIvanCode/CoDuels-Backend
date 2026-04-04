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
	"os"
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

	compiledCheckerRuntimePath string
	correctOutputRuntimePath   string
	suspectOutputRuntimePath   string
	checkVerdictRuntimePath    string
	runtimeResourceRegistry    *executor.RuntimeResourceRegistry
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
	return f.CreateWithRuntime(jb, nil, executor.NewRuntimeResourceRegistry(8))
}

func (f *CheckCppExecutorFactory) CreateWithRuntime(
	jb jobs.Job,
	rt runtime.Runtime,
	runtimeResourceRegistry *executor.RuntimeResourceRegistry,
) (executor.JobExecutor, error) {
	if jb.GetType() != job.CheckCpp {
		return nil, fmt.Errorf("unsupported job type %s for %s executor", jb.GetType(), job.CheckCpp)
	}
	if runtimeResourceRegistry == nil {
		runtimeResourceRegistry = executor.NewRuntimeResourceRegistry(8)
	}

	return &CheckCppJobExecutor{
		log:                     f.log,
		sourceProvider:          f.sourceProvider,
		outputProvider:          f.outputProvider,
		runtimeFactory:          f.runtimeFactory,
		runtime:                 rt,
		runtimeResourceRegistry: runtimeResourceRegistry,

		job: jb,
	}, nil
}

func (e *CheckCppJobExecutor) Init(ctx context.Context) error {
	if e.runtime == nil {
		rt, err := e.runtimeFactory.Create(ctx)
		if err != nil {
			return fmt.Errorf("failed to init runtime: %w", err)
		}
		e.runtime = rt
	}
	return nil
}

func (e *CheckCppJobExecutor) PrepareInput(ctx context.Context) error {
	jb := e.job.AsCheckCpp()

	compiledChecker, unlock, err := e.sourceProvider.Locate(ctx, jb.CompiledChecker.SourceID)
	if err != nil {
		return fmt.Errorf("failed to get compiled checker: %w", err)
	}
	defer unlock()

	correctOutput, unlock, err := e.sourceProvider.Locate(ctx, jb.CorrectOutput.SourceID)
	if err != nil {
		return fmt.Errorf("failed to get correct output: %w", err)
	}
	defer unlock()

	suspectOutput, unlock, err := e.sourceProvider.Locate(ctx, jb.SuspectOutput.SourceID)
	if err != nil {
		return fmt.Errorf("failed to get suspect output: %w", err)
	}
	defer unlock()

	e.compiledCheckerRuntimePath = "checker"
	if err = e.runtime.CopyToRuntime(ctx, compiledChecker, e.compiledCheckerRuntimePath); err != nil {
		return fmt.Errorf("failed to copy compiled checker to runtime: %w", err)
	}
	e.runtimeResourceRegistry.Set(jb.CompiledChecker.SourceID, e.compiledCheckerRuntimePath)

	e.correctOutputRuntimePath = "correct.txt"
	if err = e.runtime.CopyToRuntime(ctx, correctOutput, e.correctOutputRuntimePath); err != nil {
		return fmt.Errorf("failed to copy correct output to runtime: %w", err)
	}
	e.runtimeResourceRegistry.Set(jb.CorrectOutput.SourceID, e.correctOutputRuntimePath)

	e.suspectOutputRuntimePath = "suspect.txt"
	if err = e.runtime.CopyToRuntime(ctx, suspectOutput, e.suspectOutputRuntimePath); err != nil {
		return fmt.Errorf("failed to copy suspect output to runtime: %w", err)
	}
	e.runtimeResourceRegistry.Set(jb.SuspectOutput.SourceID, e.suspectOutputRuntimePath)

	return nil
}

func (e *CheckCppJobExecutor) ExecuteCommand(ctx context.Context) results.Result {
	if e.runtimeResourceRegistry == nil {
		return results.Error(e.job, fmt.Errorf("runtime resource registry is not set"))
	}

	jb := e.job.AsCheckCpp()
	checkerRuntimePath, err := e.runtimeResourceRegistry.Get(jb.CompiledChecker.SourceID)
	if err != nil {
		return results.Error(e.job, err)
	}
	correctRuntimePath, err := e.runtimeResourceRegistry.Get(jb.CorrectOutput.SourceID)
	if err != nil {
		return results.Error(e.job, err)
	}
	suspectRuntimePath, err := e.runtimeResourceRegistry.Get(jb.SuspectOutput.SourceID)
	if err != nil {
		return results.Error(e.job, err)
	}
	e.compiledCheckerRuntimePath = checkerRuntimePath
	e.correctOutputRuntimePath = correctRuntimePath
	e.suspectOutputRuntimePath = suspectRuntimePath

	stderr := bytes.NewBuffer(nil)
	e.checkVerdictRuntimePath = "verdict.txt"
	err = e.runtime.RunCommand(
		ctx,
		[]string{"./" + e.compiledCheckerRuntimePath, e.correctOutputRuntimePath, e.suspectOutputRuntimePath},
		runtime.RunParams{
			Limits: runtime.Limits{
				Memory: runtime.MemoryLimit(1024 * int64(runtime.Megabyte)),
				Time:   runtime.TimeLimit(2000 * int64(time.Millisecond)),
			},
			StdoutFile: e.checkVerdictRuntimePath,
			Stderr:     stderr,
		},
	)
	if err != nil {
		e.log.Error("execute checker in runtime error", slog.Any("err", err))
		return results.Error(e.job, fmt.Errorf("failed to execute checker: %w", err))
	}

	e.log.Info("command ok")
	executor.RegisterJobOutputRuntimePath(e.runtimeResourceRegistry, jb.GetID(), e.checkVerdictRuntimePath)

	checkVerdict, err := os.CreateTemp("/tmp", "*")
	if err != nil {
		return results.Error(e.job, fmt.Errorf("failed to create check verdict temp file: %w", err))
	}
	defer func() { _ = os.Remove(checkVerdict.Name()) }()
	defer func() { _ = checkVerdict.Close() }()

	if err = e.runtime.CopyFromRuntime(ctx, e.checkVerdictRuntimePath, checkVerdict.Name()); err != nil {
		return results.Error(e.job, fmt.Errorf("failed to copy check verdict from runtime: %w", err))
	}
	checkVerdictOutput, err := os.ReadFile(checkVerdict.Name())
	if err != nil {
		return results.Error(e.job, fmt.Errorf("failed to read check verdict: %w", err))
	}

	verdict := strings.TrimSpace(string(checkVerdictOutput))
	if verdict == string(job.StatusOK) {
		return results.NewCheckResultOK(jb.GetID())
	}
	if verdict == string(job.StatusWA) {
		return results.NewCheckResultWA(jb.GetID())
	}
	return results.Error(e.job, fmt.Errorf("failed to parse check verdict: %s", verdict))
}

func (e *CheckCppJobExecutor) SaveOutput(_ context.Context) error {
	return nil
}

func (e *CheckCppJobExecutor) Stop(ctx context.Context) error {
	if e.runtime == nil {
		return nil
	}
	return e.runtime.Stop(ctx)
}
