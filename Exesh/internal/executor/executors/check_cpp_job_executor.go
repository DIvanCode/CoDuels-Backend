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
	"io"
	"log/slog"
	"os"
	"time"
)

type CheckCppJobExecutor struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	newRuntime     runtimeFactory
	job            jobs.Job
	runtime        runtime.Runtime
	manageRuntime  bool
	inputUnlock    func()
	checkerRuntime string
	correctRuntime string
	suspectRuntime string
	verdictRuntime string
	lastResult     results.Result
}

func NewCheckCppJobExecutor(log *slog.Logger, sourceProvider sourceProvider, outputProvider outputProvider, newRuntime runtimeFactory) *CheckCppJobExecutor {
	return &CheckCppJobExecutor{log: log, sourceProvider: sourceProvider, outputProvider: outputProvider, newRuntime: newRuntime}
}
func (e *CheckCppJobExecutor) SupportsType(jobType job.Type) bool { return jobType == job.CheckCpp }
func (e *CheckCppJobExecutor) InitExecutor(jb jobs.Job) (executor.Executor, error) {
	rt, err := e.newRuntime()
	if err != nil {
		return nil, err
	}
	return e.initWithRuntime(jb, rt, true)
}
func (e *CheckCppJobExecutor) initWithRuntime(jb jobs.Job, rt runtime.Runtime, manageRuntime bool) (executor.Executor, error) {
	return &CheckCppJobExecutor{log: e.log, sourceProvider: e.sourceProvider, outputProvider: e.outputProvider, newRuntime: e.newRuntime, job: jb, runtime: rt, manageRuntime: manageRuntime}, nil
}
func (e *CheckCppJobExecutor) withSourceProvider(sp sourceProvider) initWithRuntimeExecutor {
	cp := *e
	cp.sourceProvider = sp
	return &cp
}
func (e *CheckCppJobExecutor) getSourceProvider() sourceProvider { return e.sourceProvider }
func (e *CheckCppJobExecutor) ErrorResult(err error) results.Result {
	return results.NewCheckResultErr(e.job.GetID(), err.Error())
}
func (e *CheckCppJobExecutor) PrepareInput(ctx context.Context) error {
	if e.manageRuntime {
		if err := e.runtime.InitRuntime(); err != nil {
			return err
		}
	}
	checkJob := e.job.AsCheckCpp()
	checkerRuntime, uc, err := resolveInputPath(ctx, e.sourceProvider, e.runtime, checkJob.CompiledChecker.SourceID, "checker")
	if err != nil {
		return err
	}
	correctRuntime, uco, err := resolveInputPath(ctx, e.sourceProvider, e.runtime, checkJob.CorrectOutput.SourceID, "correct")
	if err != nil {
		uc()
		return err
	}
	suspectRuntime, us, err := resolveInputPath(ctx, e.sourceProvider, e.runtime, checkJob.SuspectOutput.SourceID, "suspect")
	if err != nil {
		uco()
		uc()
		return err
	}
	e.inputUnlock = func() { us(); uco(); uc() }
	e.checkerRuntime = checkerRuntime
	e.correctRuntime = correctRuntime
	e.suspectRuntime = suspectRuntime
	e.verdictRuntime = runtimeOutputPath(e.job.GetID(), "verdict")
	return nil
}
func (e *CheckCppJobExecutor) PrepareOutput(context.Context) error { return nil }
func (e *CheckCppJobExecutor) Execute(ctx context.Context, resultsCh chan<- results.Result) results.Result {
	emit := func(res results.Result) results.Result {
		if resultsCh != nil {
			resultsCh <- res
		}
		return res
	}
	stderr := bytes.NewBuffer(nil)
	err := e.runtime.RunCommand(ctx, []string{e.checkerRuntime, e.correctRuntime, e.suspectRuntime}, runtime.RunParams{Limits: runtime.Limits{Memory: runtime.MemoryLimit(1024 * int64(runtime.Megabyte)), Time: runtime.TimeLimit(2000 * int64(time.Millisecond))}, StdoutFile: e.verdictRuntime, Stderr: stderr})
	if err != nil {
		e.lastResult = e.ErrorResult(err)
		return emit(e.lastResult)
	}
	f, err := os.CreateTemp("", "exesh-check-verdict-*")
	if err != nil {
		e.lastResult = e.ErrorResult(err)
		return emit(e.lastResult)
	}
	_ = f.Close()
	defer func() { _ = os.Remove(f.Name()) }()
	if err = e.runtime.CopyFromRuntime(e.verdictRuntime, f.Name()); err != nil {
		e.lastResult = e.ErrorResult(err)
		return emit(e.lastResult)
	}
	r, err := os.Open(f.Name())
	if err != nil {
		e.lastResult = e.ErrorResult(err)
		return emit(e.lastResult)
	}
	defer func() { _ = r.Close() }()
	out, err := io.ReadAll(r)
	if err != nil {
		e.lastResult = e.ErrorResult(err)
		return emit(e.lastResult)
	}
	if string(out) == string(job.StatusOK) {
		e.lastResult = results.NewCheckResultOK(e.job.GetID())
	} else if string(out) == string(job.StatusWA) {
		e.lastResult = results.NewCheckResultWA(e.job.GetID())
	} else {
		e.lastResult = e.ErrorResult(fmt.Errorf("failed to parse check_verdict output: %s", string(out)))
	}
	return emit(e.lastResult)
}
func (e *CheckCppJobExecutor) StopExecutor(context.Context) error {
	return cleanupExecutor(e.inputUnlock, e.manageRuntime, e.runtime)
}
