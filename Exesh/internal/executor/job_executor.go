package executor

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"fmt"
)

type (
	ExecutorFactory struct {
		executorFactories []executorFactory
	}

	JobExecutor interface {
		Init(context.Context) error
		PrepareInput(context.Context) error
		ExecuteCommand(context.Context) results.Result
		SaveOutput(context.Context) error
		Stop(context.Context) error
	}

	executorFactory interface {
		SupportsType(job.Type) bool
		Create(jobs.Job) (JobExecutor, error)
	}
)

func NewExecutorFactory(executorFactories ...executorFactory) *ExecutorFactory {
	return &ExecutorFactory{executorFactories: executorFactories}
}

func (f *ExecutorFactory) Create(jb jobs.Job) (JobExecutor, error) {
	for _, factory := range f.executorFactories {
		if factory.SupportsType(jb.GetType()) {
			return factory.Create(jb)
		}
	}
	return nil, fmt.Errorf("unsupported job type %s", jb.GetType())
}
