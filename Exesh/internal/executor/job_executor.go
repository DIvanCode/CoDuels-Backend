package executor

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/runtime"
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
		SaveOutput(context.Context, *results.Result) error
		Stop(context.Context) error
	}

	executorFactory interface {
		SupportsType(job.Type) bool
		Create(jobs.Job) (JobExecutor, error)
		CreateWithRuntime(jobs.Job, runtime.Runtime, *RuntimeResourceRegistry) (JobExecutor, error)
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

func (f *ExecutorFactory) CreateWithRuntime(
	jb jobs.Job,
	rt runtime.Runtime,
	runtimeResourceRegistry *RuntimeResourceRegistry,
) (JobExecutor, error) {
	for _, factory := range f.executorFactories {
		if factory.SupportsType(jb.GetType()) {
			return factory.CreateWithRuntime(jb, rt, runtimeResourceRegistry)
		}
	}
	return nil, fmt.Errorf("unsupported job type %s", jb.GetType())
}
