package provider

import (
	"context"
	"exesh/internal/domain/execution"
	"fmt"
	"io"
)

type (
	OutputProvider struct {
		providers []outputProvider
	}

	outputProvider interface {
		SupportsType(execution.OutputType) bool
		Create(context.Context, execution.Output) (w io.Writer, commit, abort func() error, err error)
		Locate(context.Context, execution.Output) (path string, unlock func(), err error)
		Read(context.Context, execution.Output) (r io.Reader, unlock func(), err error)
	}
)

func NewOutputProvider(providers ...outputProvider) *OutputProvider {
	return &OutputProvider{providers: providers}
}

func (p *OutputProvider) Create(ctx context.Context, output execution.Output) (w io.Writer, commit, abort func() error, err error) {
	for _, provider := range p.providers {
		if provider.SupportsType(output.GetType()) {
			return provider.Create(ctx, output)
		}
	}
	err = fmt.Errorf("provider for %s iutput not found", output.GetType())
	return
}

func (p *OutputProvider) Locate(ctx context.Context, output execution.Output) (path string, unlock func(), err error) {
	for _, provider := range p.providers {
		if provider.SupportsType(output.GetType()) {
			return provider.Locate(ctx, output)
		}
	}
	err = fmt.Errorf("provider for %s output not found", output.GetType())
	return
}

func (p *OutputProvider) Read(ctx context.Context, output execution.Output) (r io.Reader, unlock func(), err error) {
	for _, provider := range p.providers {
		if provider.SupportsType(output.GetType()) {
			return provider.Read(ctx, output)
		}
	}
	err = fmt.Errorf("provider for %s output not found", output.GetType())
	return
}
