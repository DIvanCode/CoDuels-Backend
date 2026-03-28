package executors

import (
	"context"
	"errors"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source"
	"exesh/internal/executor"
	"exesh/internal/runtime"
	"io"
	"path/filepath"
)

type (
	runtimeFactory func() (runtime.Runtime, error)

	initWithRuntimeExecutor interface {
		executor.Executor
		initWithRuntime(jobs.Job, runtime.Runtime, bool) (executor.Executor, error)
		withSourceProvider(sourceProvider) initWithRuntimeExecutor
		getSourceProvider() sourceProvider
	}

	sourceProvider interface {
		Locate(context.Context, source.ID) (path string, unlock func(), err error)
		RuntimePath(source.ID) (path string, ok bool)
	}

	outputProvider interface {
		Reserve(context.Context, job.ID, string) (path string, commit, abort func() error, err error)
		Read(context.Context, job.ID, string) (r io.Reader, unlock func(), err error)
	}
)

func noopAction() error {
	return nil
}

func noopUnlock() {}

func runtimeInputPath(id source.ID, file string) string {
	return filepath.Join("inputs", id.String()+"_"+filepath.Base(file))
}

func runtimeOutputPath(id job.ID, file string) string {
	return id.String() + "_" + filepath.Base(file)
}

func resolveInputPath(
	ctx context.Context,
	sp sourceProvider,
	rt runtime.Runtime,
	sourceID source.ID,
	file string,
) (runtimePath string, unlock func(), err error) {
	if runtimePath, ok := sp.RuntimePath(sourceID); ok {
		return runtimePath, noopUnlock, nil
	}

	path, unlock, err := sp.Locate(ctx, sourceID)
	if err != nil {
		return "", nil, err
	}

	runtimePath = runtimeInputPath(sourceID, file)
	if err = rt.CopyToRuntime(path, runtimePath); err != nil {
		unlock()
		return "", nil, err
	}
	unlock()

	return runtimePath, noopUnlock, nil
}

func finalizeExecutor(
	lastResult results.Result,
	successStatus job.Status,
	outputCommit func() error,
	outputAbort func() error,
	inputUnlock func(),
	manageRuntime bool,
	rt runtime.Runtime,
) error {
	var err error
	if lastResult.GetError() == nil && lastResult.GetStatus() == successStatus {
		err = outputCommit()
	} else {
		err = outputAbort()
	}

	if inputUnlock != nil {
		inputUnlock()
	}
	if manageRuntime && rt != nil {
		err = errors.Join(err, rt.StopRuntime())
	}

	return err
}

func cleanupExecutor(inputUnlock func(), manageRuntime bool, rt runtime.Runtime) error {
	if inputUnlock != nil {
		inputUnlock()
	}
	if manageRuntime && rt != nil {
		return rt.StopRuntime()
	}
	return nil
}

type chainSourceProvider struct {
	base         sourceProvider
	runtimePaths map[source.ID]string
}

func (p *chainSourceProvider) Locate(ctx context.Context, sourceID source.ID) (path string, unlock func(), err error) {
	return p.base.Locate(ctx, sourceID)
}

func (p *chainSourceProvider) RuntimePath(sourceID source.ID) (path string, ok bool) {
	path, ok = p.runtimePaths[sourceID]
	return path, ok
}

func fallbackErrorResult(jb jobs.Job, err error) results.Result {
	switch jb.GetType() {
	case job.CompileCpp, job.CompileGo:
		return results.NewCompileResultErr(jb.GetID(), err.Error())
	case job.RunCpp, job.RunGo, job.RunPy:
		return results.NewRunResultErr(jb.GetID(), err.Error())
	case job.CheckCpp:
		return results.NewCheckResultErr(jb.GetID(), err.Error())
	case job.Chain:
		chainJob := jb.AsChain()
		if len(chainJob.Jobs) == 0 {
			return results.NewChainResultErr(jb.GetID(), nil, err.Error())
		}
		return results.NewChainResultErr(
			jb.GetID(),
			[]results.Result{fallbackErrorResult(chainJob.Jobs[len(chainJob.Jobs)-1], err)},
			err.Error(),
		)
	default:
		return results.NewRunResultErr(jb.GetID(), err.Error())
	}
}
