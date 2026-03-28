package executors

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source"
	"exesh/internal/executor"
	"exesh/internal/runtime"
	"fmt"
	"log/slog"
)

type ChainJobExecutor struct {
	log        *slog.Logger
	newRuntime runtimeFactory
	executors  []initWithRuntimeExecutor
	job        jobs.Job
	runtime    runtime.Runtime
	inner      []executor.Executor
}

func NewChainJobExecutor(log *slog.Logger, newRuntime runtimeFactory, executors ...initWithRuntimeExecutor) *ChainJobExecutor {
	return &ChainJobExecutor{log: log, newRuntime: newRuntime, executors: executors}
}

func (e *ChainJobExecutor) SupportsType(jobType job.Type) bool { return jobType == job.Chain }
func (e *ChainJobExecutor) InitExecutor(jb jobs.Job) (executor.Executor, error) {
	return &ChainJobExecutor{log: e.log, newRuntime: e.newRuntime, executors: e.executors, job: jb, inner: make([]executor.Executor, 0)}, nil
}
func (e *ChainJobExecutor) ErrorResult(err error) results.Result {
	chainJob := e.job.AsChain()
	if len(chainJob.Jobs) == 0 {
		return results.NewChainResultErr(e.job.GetID(), nil, err.Error())
	}
	return results.NewChainResultErr(e.job.GetID(), []results.Result{fallbackErrorResult(chainJob.Jobs[len(chainJob.Jobs)-1], err)}, err.Error())
}

func (e *ChainJobExecutor) PrepareInput(ctx context.Context) error {
	rt, err := e.newRuntime()
	if err != nil {
		return err
	}
	if err = rt.InitRuntime(); err != nil {
		return err
	}
	e.runtime = rt

	chainJob := e.job.AsChain()
	runtimePaths := make(map[source.ID]string, len(chainJob.Jobs))
	for _, chainStep := range chainJob.Jobs {
		if out := chainStep.GetOutput(); out != nil {
			runtimePaths[source.ID(chainStep.GetID())] = runtimeOutputPath(chainStep.GetID(), out.File)
		}
	}
	for _, chainStep := range chainJob.Jobs {
		template, err := e.selectExecutor(chainStep.GetType())
		if err != nil {
			return err
		}
		instance, err := template.withSourceProvider(&chainSourceProvider{
			base:         template.getSourceProvider(),
			runtimePaths: runtimePaths,
		}).initWithRuntime(chainStep, rt, false)
		if err != nil {
			return err
		}
		if err = instance.PrepareInput(ctx); err != nil {
			return err
		}
		e.inner = append(e.inner, instance)
	}
	return nil
}

func (e *ChainJobExecutor) PrepareOutput(context.Context) error { return nil }

func (e *ChainJobExecutor) Execute(ctx context.Context, resultsCh chan<- results.Result) results.Result {
	chainJob := e.job.AsChain()
	innerResults := make([]results.Result, 0, len(chainJob.Jobs))
	for i, inner := range e.inner {
		if i == len(e.inner)-1 {
			if err := inner.PrepareOutput(ctx); err != nil {
				innerResults = append(innerResults, inner.ErrorResult(err))
				return e.chainResultFromInner(innerResults)
			}
		}

		res := inner.Execute(ctx, resultsCh)
		innerResults = append(innerResults, res)
		if err := inner.StopExecutor(ctx); err != nil {
			innerResults[len(innerResults)-1] = inner.ErrorResult(err)
			return e.chainResultFromInner(innerResults)
		}
		if res.GetError() != nil || res.GetStatus() != chainJob.Jobs[i].GetSuccessStatus() {
			return e.chainResultFromInner(innerResults)
		}
	}
	return e.chainResultFromInner(innerResults)
}

func (e *ChainJobExecutor) StopExecutor(context.Context) error {
	if e.runtime != nil {
		return e.runtime.StopRuntime()
	}
	return nil
}

func (e *ChainJobExecutor) selectExecutor(jobType job.Type) (initWithRuntimeExecutor, error) {
	for _, executor := range e.executors {
		if executor.SupportsType(jobType) {
			return executor, nil
		}
	}
	return nil, fmt.Errorf("unsupported job type %s", jobType)
}

func (e *ChainJobExecutor) chainResultFromInner(inner []results.Result) results.Result {
	if len(inner) == 0 {
		return results.NewChainResultErr(e.job.GetID(), nil, "empty chain result")
	}
	last := inner[len(inner)-1]
	if last.GetError() != nil {
		return results.NewChainResultErr(e.job.GetID(), inner, last.GetError().Error())
	}
	return results.NewChainResult(e.job.GetID(), last.GetStatus(), inner)
}
