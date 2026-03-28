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
	errs "github.com/DIvanCode/filestorage/pkg/errors"
	"io"
	"log/slog"
	"os"
	"time"
)

type RunCppJobExecutor struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	newRuntime     runtimeFactory
	job            jobs.Job
	runtime        runtime.Runtime
	manageRuntime  bool
	inputUnlock    func()
	outputCommit   func() error
	outputAbort    func() error
	binaryRuntime  string
	stdinRuntime   string
	outputRuntime  string
	outputHost     string
	alreadyExists  bool
	lastResult     results.Result
}

func NewRunCppJobExecutor(log *slog.Logger, sourceProvider sourceProvider, outputProvider outputProvider, newRuntime runtimeFactory) *RunCppJobExecutor {
	return &RunCppJobExecutor{log: log, sourceProvider: sourceProvider, outputProvider: outputProvider, newRuntime: newRuntime}
}
func (e *RunCppJobExecutor) SupportsType(jobType job.Type) bool { return jobType == job.RunCpp }
func (e *RunCppJobExecutor) InitExecutor(jb jobs.Job) (executor.Executor, error) {
	rt, err := e.newRuntime()
	if err != nil {
		return nil, err
	}
	return e.initWithRuntime(jb, rt, true)
}
func (e *RunCppJobExecutor) initWithRuntime(jb jobs.Job, rt runtime.Runtime, manageRuntime bool) (executor.Executor, error) {
	return &RunCppJobExecutor{log: e.log, sourceProvider: e.sourceProvider, outputProvider: e.outputProvider, newRuntime: e.newRuntime, job: jb, runtime: rt, manageRuntime: manageRuntime, outputCommit: noopAction, outputAbort: noopAction}, nil
}
func (e *RunCppJobExecutor) withSourceProvider(sp sourceProvider) initWithRuntimeExecutor {
	cp := *e
	cp.sourceProvider = sp
	return &cp
}
func (e *RunCppJobExecutor) getSourceProvider() sourceProvider { return e.sourceProvider }
func (e *RunCppJobExecutor) ErrorResult(err error) results.Result {
	return results.NewRunResultErr(e.job.GetID(), err.Error())
}
func (e *RunCppJobExecutor) PrepareInput(ctx context.Context) error {
	if e.manageRuntime {
		if err := e.runtime.InitRuntime(); err != nil {
			return err
		}
	}
	runCppJob := e.job.AsRunCpp()
	binRuntime, unlockBin, err := resolveInputPath(ctx, e.sourceProvider, e.runtime, runCppJob.CompiledCode.SourceID, "program")
	if err != nil {
		return err
	}
	inRuntime, unlockIn, err := resolveInputPath(ctx, e.sourceProvider, e.runtime, runCppJob.RunInput.SourceID, "stdin")
	if err != nil {
		unlockBin()
		return err
	}
	e.inputUnlock = func() { unlockIn(); unlockBin() }
	e.binaryRuntime = binRuntime
	e.stdinRuntime = inRuntime
	e.outputRuntime = runtimeOutputPath(e.job.GetID(), runCppJob.RunOutput.File)
	return nil
}
func (e *RunCppJobExecutor) PrepareOutput(ctx context.Context) error {
	runCppJob := e.job.AsRunCpp()
	path, commit, abort, err := e.outputProvider.Reserve(ctx, e.job.GetID(), runCppJob.RunOutput.File)
	if errors.Is(err, errs.ErrFileAlreadyExists) {
		e.alreadyExists = true
		return nil
	}
	if err != nil {
		return err
	}
	e.outputHost = path
	e.outputCommit = func() error {
		if err := e.runtime.CopyFromRuntime(e.outputRuntime, e.outputHost); err != nil {
			_ = abort()
			return err
		}
		if err := commit(); err != nil {
			_ = abort()
			return err
		}
		return nil
	}
	e.outputAbort = abort
	return nil
}
func (e *RunCppJobExecutor) Execute(ctx context.Context, resultsCh chan<- results.Result) results.Result {
	emit := func(res results.Result) results.Result {
		if resultsCh != nil {
			resultsCh <- res
		}
		return res
	}
	runCppJob := e.job.AsRunCpp()
	if !e.alreadyExists {
		stderr := bytes.NewBuffer(nil)
		err := e.runtime.RunCommand(ctx, []string{e.binaryRuntime}, runtime.RunParams{
			Limits:    runtime.Limits{Memory: runtime.MemoryLimit(int64(runCppJob.MemoryLimit) * int64(runtime.Megabyte)), Time: runtime.TimeLimit(int64(runCppJob.TimeLimit) * int64(time.Millisecond))},
			StdinFile: e.stdinRuntime, StdoutFile: e.outputRuntime, Stderr: stderr,
		})
		if err != nil {
			e.log.Error("execute binary in runtime error", slog.Any("err", err))
			if errors.Is(err, runtime.ErrTimeout) {
				e.lastResult = results.NewRunResultTL(e.job.GetID())
			} else if errors.Is(err, runtime.ErrOutOfMemory) {
				e.lastResult = results.NewRunResultML(e.job.GetID())
			} else {
				e.lastResult = results.NewRunResultRE(e.job.GetID())
			}
			return emit(e.lastResult)
		}
	}
	if !runCppJob.ShowOutput {
		e.lastResult = results.NewRunResultOK(e.job.GetID())
		return emit(e.lastResult)
	}
	outputPath := e.outputHost
	temp := ""
	if outputPath == "" {
		f, err := os.CreateTemp("", "exesh-run-output-*")
		if err != nil {
			e.lastResult = e.ErrorResult(err)
			return emit(e.lastResult)
		}
		_ = f.Close()
		outputPath = f.Name()
		temp = outputPath
	}
	if err := e.runtime.CopyFromRuntime(e.outputRuntime, outputPath); err != nil {
		e.lastResult = e.ErrorResult(err)
		return emit(e.lastResult)
	}
	if temp != "" {
		defer func() { _ = os.Remove(temp) }()
	}
	r, err := os.Open(outputPath)
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
	e.lastResult = results.NewRunResultWithOutput(e.job.GetID(), string(out))
	return emit(e.lastResult)
}
func (e *RunCppJobExecutor) StopExecutor(context.Context) error {
	if e.alreadyExists {
		return cleanupExecutor(e.inputUnlock, e.manageRuntime, e.runtime)
	}
	return finalizeExecutor(
		e.lastResult,
		e.job.GetSuccessStatus(),
		e.outputCommit,
		e.outputAbort,
		e.inputUnlock,
		e.manageRuntime,
		e.runtime,
	)
}
