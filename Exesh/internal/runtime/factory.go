package runtime

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"fmt"
)

type (
	RuntimeFactory interface {
		SupportsType(job.Type) bool
		Create(context.Context) (Runtime, error)
	}

	JobRuntimeFactory struct {
		runtimeFactories []RuntimeFactory
	}
)

func NewJobRuntimeFactory(runtimeFactories ...RuntimeFactory) *JobRuntimeFactory {
	return &JobRuntimeFactory{runtimeFactories: runtimeFactories}
}

func (f *JobRuntimeFactory) Create(ctx context.Context, jb jobs.Job) (Runtime, error) {
	for _, runtimeFactory := range f.runtimeFactories {
		if runtimeFactory.SupportsType(jb.GetType()) {
			return runtimeFactory.Create(ctx)
		}
	}
	return nil, fmt.Errorf("unsupported job type %s", jb.GetType())
}
