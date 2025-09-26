package provider

import (
	"context"
	"exesh/internal/domain/graph"
	"fmt"
	"io"
)

type (
	InputProvider struct {
		providers []inputProvider
	}

	inputProvider interface {
		SupportsType(graph.InputType) bool
		Get(context.Context, graph.Input) (r io.Reader, unlock func(), err error)
	}
)

func NewInputProvider(providers ...inputProvider) *InputProvider {
	return &InputProvider{providers: providers}
}

func (p *InputProvider) Get(ctx context.Context, input graph.Input) (r io.Reader, unlock func(), err error) {
	for _, provider := range p.providers {
		if provider.SupportsType(input.GetType()) {
			return provider.Get(ctx, input)
		}
	}
	err = fmt.Errorf("provider for %s input not found", input.GetType())
	return
}
