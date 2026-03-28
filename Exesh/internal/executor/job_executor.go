package executor

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"fmt"
)

type (
	Executor interface {
		SupportsType(job.Type) bool
		InitExecutor(jobs.Job) (Executor, error)
		PrepareInput(context.Context) error
		PrepareOutput(context.Context) error
		Execute(context.Context, chan<- results.Result) results.Result
		StopExecutor(context.Context) error
		ErrorResult(error) results.Result
	}

	JobExecutor struct {
		executors []Executor
	}
)

func NewJobExecutor(executors ...Executor) *JobExecutor {
	return &JobExecutor{executors: executors}
}

func (e *JobExecutor) InitExecutor(jb jobs.Job) (Executor, error) {
	executor, err := e.selectExecutor(jb.GetType())
	if err != nil {
		return nil, err
	}

	return executor.InitExecutor(jb)
}

func (e *JobExecutor) ErrorResult(jb jobs.Job, err error) results.Result {
	executor, selectErr := e.selectExecutor(jb.GetType())
	if selectErr != nil {
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
				[]results.Result{fallbackTypedErrorResult(chainJob.Jobs[len(chainJob.Jobs)-1], err)},
				err.Error(),
			)
		default:
			return results.NewRunResultErr(jb.GetID(), err.Error())
		}
	}

	instance, err := executor.InitExecutor(jb)
	if err != nil {
		return fallbackTypedErrorResult(jb, err)
	}

	return instance.ErrorResult(err)
}

func (e *JobExecutor) selectExecutor(jobType job.Type) (Executor, error) {
	for _, executor := range e.executors {
		if executor.SupportsType(jobType) {
			return executor, nil
		}
	}

	return nil, fmt.Errorf("unsupported job type %s", jobType)
}

func fallbackTypedErrorResult(jb jobs.Job, err error) results.Result {
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
			[]results.Result{fallbackTypedErrorResult(chainJob.Jobs[len(chainJob.Jobs)-1], err)},
			err.Error(),
		)
	default:
		return results.NewRunResultErr(jb.GetID(), err.Error())
	}
}
