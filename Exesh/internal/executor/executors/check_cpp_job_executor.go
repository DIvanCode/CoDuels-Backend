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
	runtime        runtime.Runtime
	runtimeID      runtime.ID

	job jobs.Job

	compiledCheckerRuntimePath string
	correctOutputRuntimePath   string
	suspectOutputRuntimePath   string
	checkVerdictRuntimePath    string
}

type CheckCppExecutorFactory struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	runtime        runtime.Runtime
}

func NewCheckCppExecutorFactory(log *slog.Logger, sourceProvider sourceProvider, outputProvider outputProvider, rt runtime.Runtime) *CheckCppExecutorFactory {
	return &CheckCppExecutorFactory{
		log:            log,
		sourceProvider: sourceProvider,
		outputProvider: outputProvider,
		runtime:        rt,
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
		runtime:        f.runtime,

		job: jb,
	}, nil
}

func (e *CheckCppJobExecutor) Init(ctx context.Context) error {
	runtimeID, err := e.runtime.Init(ctx)
	if err != nil {
		return fmt.Errorf("failed to init runtime: %w", err)
	}
	e.runtimeID = runtimeID
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
	if err = e.runtime.CopyToRuntime(ctx, e.runtimeID, compiledChecker, e.compiledCheckerRuntimePath); err != nil {
		return fmt.Errorf("failed to copy compiled checker to runtime: %w", err)
	}

	e.correctOutputRuntimePath = "correct.txt"
	if err = e.runtime.CopyToRuntime(ctx, e.runtimeID, correctOutput, e.correctOutputRuntimePath); err != nil {
		return fmt.Errorf("failed to copy correct output to runtime: %w", err)
	}

	e.suspectOutputRuntimePath = "suspect.txt"
	if err = e.runtime.CopyToRuntime(ctx, e.runtimeID, suspectOutput, e.suspectOutputRuntimePath); err != nil {
		return fmt.Errorf("failed to copy suspect output to runtime: %w", err)
	}

	return nil
}

func (e *CheckCppJobExecutor) ExecuteCommand(ctx context.Context) results.Result {
	jb := e.job.AsCheckCpp()

	stderr := bytes.NewBuffer(nil)
	e.checkVerdictRuntimePath = "verdict.txt"
	err := e.runtime.RunCommand(
		ctx,
		e.runtimeID,
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

	checkVerdict, err := os.CreateTemp("/tmp", "*")
	if err != nil {
		return results.Error(e.job, fmt.Errorf("failed to create check verdict temp file: %w", err))
	}
	defer func() { _ = os.Remove(checkVerdict.Name()) }()
	defer func() { _ = checkVerdict.Close() }()

	if err = e.runtime.CopyFromRuntime(ctx, e.runtimeID, e.checkVerdictRuntimePath, checkVerdict.Name()); err != nil {
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
	return e.runtime.Stop(ctx, e.runtimeID)
}
