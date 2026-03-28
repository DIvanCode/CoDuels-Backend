package executors

import (
	"bytes"
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/executor"
	"exesh/internal/runtime"
	"log/slog"
	"time"
)

type CompileCppJobExecutor struct {
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
	outputHost    string
	outputRuntime string
	lastResult    results.Result
}

func NewCompileCppJobExecutor(log *slog.Logger, sourceProvider sourceProvider, outputProvider outputProvider, newRuntime runtimeFactory) *CompileCppJobExecutor {
	return &CompileCppJobExecutor{
		log:            log,
		sourceProvider: sourceProvider,
		outputProvider: outputProvider,
		newRuntime:     newRuntime,
	}
}

func (e *CompileCppJobExecutor) SupportsType(jobType job.Type) bool {
	return jobType == job.CompileCpp
}

func (e *CompileCppJobExecutor) InitExecutor(jb jobs.Job) (executor.Executor, error) {
	rt, err := e.newRuntime()
	if err != nil {
		return nil, err
	}

	return e.initWithRuntime(jb, rt, true)
}

func (e *CompileCppJobExecutor) initWithRuntime(jb jobs.Job, rt runtime.Runtime, manageRuntime bool) (executor.Executor, error) {
	return &CompileCppJobExecutor{
		log:            e.log,
		sourceProvider: e.sourceProvider,
		outputProvider: e.outputProvider,
		newRuntime:     e.newRuntime,
		job:            jb,
		runtime:        rt,
		manageRuntime:  manageRuntime,
		outputCommit:   noopAction,
		outputAbort:    noopAction,
	}, nil
}
func (e *CompileCppJobExecutor) withSourceProvider(sp sourceProvider) initWithRuntimeExecutor {
	cp := *e
	cp.sourceProvider = sp
	return &cp
}
func (e *CompileCppJobExecutor) getSourceProvider() sourceProvider { return e.sourceProvider }

func (e *CompileCppJobExecutor) ErrorResult(err error) results.Result {
	return results.NewCompileResultErr(e.job.GetID(), err.Error())
}

func (e *CompileCppJobExecutor) PrepareInput(ctx context.Context) error {
	if e.manageRuntime {
		if err := e.runtime.InitRuntime(); err != nil {
			return err
		}
	}

	compileCppJob := e.job.AsCompileCpp()
	codeRuntime, unlock, err := resolveInputPath(ctx, e.sourceProvider, e.runtime, compileCppJob.Code.SourceID, compileCppJob.CompiledCode.File+".cpp")
	if err != nil {
		return err
	}
	e.inputUnlock = unlock
	e.codeRuntime = codeRuntime

	e.outputRuntime = runtimeOutputPath(e.job.GetID(), compileCppJob.CompiledCode.File)
	return nil
}

func (e *CompileCppJobExecutor) PrepareOutput(ctx context.Context) error {
	compileCppJob := e.job.AsCompileCpp()

	path, commit, abort, err := e.outputProvider.Reserve(ctx, e.job.GetID(), compileCppJob.CompiledCode.File)
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

func (e *CompileCppJobExecutor) Execute(ctx context.Context, resultsCh chan<- results.Result) results.Result {
	emit := func(res results.Result) results.Result {
		if resultsCh != nil {
			resultsCh <- res
		}
		return res
	}
	compileCppJob := e.job.AsCompileCpp()
	stderr := bytes.NewBuffer(nil)
	err := e.runtime.RunCommand(ctx,
		[]string{"g++", e.codeRuntime, "-o", e.outputRuntime},
		runtime.RunParams{
			Limits: runtime.Limits{
				Memory: runtime.MemoryLimit(1024 * int64(runtime.Megabyte)),
				Time:   runtime.TimeLimit(30000 * int64(time.Millisecond)),
			},
			Stderr: stderr,
		})
	if err != nil {
		e.log.Error("execute g++ in runtime error", slog.Any("err", err))
		e.lastResult = results.NewCompileResultCE(e.job.GetID(), stderr.String())
		return emit(e.lastResult)
	}

	e.lastResult = results.NewCompileResultOK(compileCppJob.GetID())
	return emit(e.lastResult)
}

func (e *CompileCppJobExecutor) StopExecutor(context.Context) error {
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
