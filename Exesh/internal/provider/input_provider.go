package provider

import (
	"context"
	"exesh/internal/domain/execution"
	"fmt"
	"io"
)

type (
	InputProvider struct {
		providers []inputProvider
	}

	inputProvider interface {
		SupportsType(execution.InputType) bool
		Create(context.Context, execution.Input) (w io.Writer, commit, abort func() error, err error)
		Locate(context.Context, execution.Input) (path string, unlock func(), err error)
		Read(context.Context, execution.Input) (r io.Reader, unlock func(), err error)
	}
)

func NewInputProvider(providers ...inputProvider) *InputProvider {
	return &InputProvider{providers: providers}
}

func (p *InputProvider) Create(ctx context.Context, input execution.Input) (w io.Writer, commit, abort func() error, err error) {
	for _, provider := range p.providers {
		if provider.SupportsType(input.GetType()) {
			return provider.Create(ctx, input)
		}
	}
	err = fmt.Errorf("provider for %s input not found", input.GetType())
	return
}

func (p *InputProvider) Locate(ctx context.Context, input execution.Input) (path string, unlock func(), err error) {
	for _, provider := range p.providers {
		if provider.SupportsType(input.GetType()) {
			return provider.Locate(ctx, input)
		}
	}
	err = fmt.Errorf("provider for %s input not found", input.GetType())
	return
}

func (p *InputProvider) Read(ctx context.Context, input execution.Input) (r io.Reader, unlock func(), err error) {
	for _, provider := range p.providers {
		if provider.SupportsType(input.GetType()) {
			return provider.Read(ctx, input)
		}
	}
	err = fmt.Errorf("provider for %s input not found", input.GetType())
	return
}
