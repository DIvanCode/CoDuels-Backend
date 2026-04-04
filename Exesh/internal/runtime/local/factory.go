package local

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/runtime"
)

type RuntimeFactory struct {
	jobTypes map[job.Type]struct{}
}

func NewRuntimeFactory(jobTypes ...job.Type) *RuntimeFactory {
	jobTypesSet := make(map[job.Type]struct{}, len(jobTypes))
	for _, jobType := range jobTypes {
		jobTypesSet[jobType] = struct{}{}
	}
	return &RuntimeFactory{jobTypes: jobTypesSet}
}

func (f *RuntimeFactory) SupportsType(jobType job.Type) bool {
	_, ok := f.jobTypes[jobType]
	return ok
}

func (f *RuntimeFactory) Create(ctx context.Context) (runtime.Runtime, error) {
	rt := New()
	if err := rt.Init(ctx); err != nil {
		return nil, err
	}
	return rt, nil
}
