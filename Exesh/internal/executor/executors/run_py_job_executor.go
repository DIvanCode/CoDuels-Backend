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
	"io"
	"log/slog"
	"os"
	"time"
)

type RunPyJobExecutor struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	newRuntime     runtimeFactory

	job jobs.Job

	runtime       runtime.Runtime
	manageRuntime bool
	inputUnlock   func()
	outputCommit  func() error
	outputAbort   func() error
	codeRuntime   string
	stdinRuntime  string
	outputRuntime string
	outputHost    string
	alreadyExists bool
	lastResult    results.Result
}

func NewRunPyJobExecutor(log *slog.Logger, sourceProvider sourceProvider, outputProvider outputProvider, newRuntime runtimeFactory) *RunPyJobExecutor {
	return &RunPyJobExecutor{log: log, sourceProvider: sourceProvider, outputProvider: outputProvider, newRuntime: newRuntime}
}

func (e *RunPyJobExecutor) SupportsType(jobType job.Type) bool { return jobType == job.RunPy }

func (e *RunPyJobExecutor) InitExecutor(jb jobs.Job) (executor.Executor, error) {
	rt, err := e.newRuntime()
	if err != nil {
		return nil, err
	}
	return e.initWithRuntime(jb, rt, true)
}

func (e *RunPyJobExecutor) initWithRuntime(jb jobs.Job, rt runtime.Runtime, manageRuntime bool) (executor.Executor, error) {
	return &RunPyJobExecutor{
		log: e.log, sourceProvider: e.sourceProvider, outputProvider: e.outputProvider, newRuntime: e.newRuntime,
		job: jb, runtime: rt, manageRuntime: manageRuntime, outputCommit: noopAction, outputAbort: noopAction,
	}, nil
}
func (e *RunPyJobExecutor) withSourceProvider(sp sourceProvider) initWithRuntimeExecutor {
	cp := *e
	cp.sourceProvider = sp
	return &cp
}
func (e *RunPyJobExecutor) getSourceProvider() sourceProvider { return e.sourceProvider }

func (e *RunPyJobExecutor) ErrorResult(err error) results.Result {
	return results.NewRunResultErr(e.job.GetID(), err.Error())
}

func (e *RunPyJobExecutor) PrepareInput(ctx context.Context) error {
	if e.manageRuntime {
		if err := e.runtime.InitRuntime(); err != nil {
			return err
		}
	}
	runPyJob := e.job.AsRunPy()
	codeRuntime, unlockCode, err := resolveInputPath(ctx, e.sourceProvider, e.runtime, runPyJob.Code.SourceID, "main.py")
	if err != nil {
		return err
	}
	stdinRuntime, unlockRunInput, err := resolveInputPath(ctx, e.sourceProvider, e.runtime, runPyJob.RunInput.SourceID, "stdin")
	if err != nil {
		unlockCode()
		return err
	}
	e.inputUnlock = func() {
		unlockRunInput()
		unlockCode()
	}
	e.codeRuntime = codeRuntime
	e.stdinRuntime = stdinRuntime
	e.outputRuntime = runtimeOutputPath(e.job.GetID(), runPyJob.RunOutput.File)
	return nil
}

func (e *RunPyJobExecutor) PrepareOutput(ctx context.Context) error {
	runPyJob := e.job.AsRunPy()
	path, commit, abort, err := e.outputProvider.Reserve(ctx, e.job.GetID(), runPyJob.RunOutput.File)
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

func (e *RunPyJobExecutor) Execute(ctx context.Context, resultsCh chan<- results.Result) results.Result {
	emit := func(res results.Result) results.Result {
		if resultsCh != nil {
			resultsCh <- res
		}
		return res
	}
	runPyJob := e.job.AsRunPy()
	if !e.alreadyExists {
		stderr := bytes.NewBuffer(nil)
		err := e.runtime.RunCommand(ctx,
			[]string{"/usr/bin/python3", e.codeRuntime},
			runtime.RunParams{
				Limits: runtime.Limits{
					Memory: runtime.MemoryLimit(int64(runPyJob.MemoryLimit) * int64(runtime.Megabyte)),
					Time:   runtime.TimeLimit(int64(runPyJob.TimeLimit) * int64(time.Millisecond)),
				},
				StdinFile:  e.stdinRuntime,
				StdoutFile: e.outputRuntime,
				Stderr:     stderr,
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

	if !runPyJob.ShowOutput {
		e.lastResult = results.NewRunResultOK(e.job.GetID())
		return emit(e.lastResult)
	}

	outputPath := e.outputHost
	tempPath := ""
	if outputPath == "" {
		f, err := os.CreateTemp("", "exesh-run-output-*")
		if err != nil {
			e.lastResult = e.ErrorResult(fmt.Errorf("failed to create temp output: %w", err))
			return emit(e.lastResult)
		}
		_ = f.Close()
		outputPath = f.Name()
		tempPath = outputPath
	}
	if tempPath != "" {
		defer func() { _ = os.Remove(tempPath) }()
	}

	if err := e.runtime.CopyFromRuntime(e.outputRuntime, outputPath); err != nil {
		e.lastResult = e.ErrorResult(fmt.Errorf("failed to copy run output: %w", err))
		return emit(e.lastResult)
	}

	runOutputReader, err := os.Open(outputPath)
	if err != nil {
		e.lastResult = e.ErrorResult(fmt.Errorf("failed to open run output: %w", err))
		return emit(e.lastResult)
	}
	defer func() { _ = runOutputReader.Close() }()

	out, err := io.ReadAll(runOutputReader)
	if err != nil {
		e.lastResult = e.ErrorResult(fmt.Errorf("failed to read run output: %w", err))
		return emit(e.lastResult)
	}

	e.lastResult = results.NewRunResultWithOutput(e.job.GetID(), string(out))
	return emit(e.lastResult)
}

func (e *RunPyJobExecutor) StopExecutor(context.Context) error {
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
