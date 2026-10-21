package local

import (
	"context"
	"exesh/internal/runtime"
)

type RuntimeFactory struct{}

func NewRuntimeFactory() *RuntimeFactory {
	return &RuntimeFactory{}
}

func (f *RuntimeFactory) Create(ctx context.Context) (runtime.Runtime, error) {
	rt := New()
	if err := rt.Init(ctx); err != nil {
		return nil, err
	}
	return rt, nil
}
