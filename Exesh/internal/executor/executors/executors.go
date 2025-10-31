package executors

import (
	"context"
	"exesh/internal/domain/execution"
	"io"
)

type (
	inputProvider interface {
		Locate(context.Context, execution.Input) (path string, unlock func(), err error)
		Read(context.Context, execution.Input) (r io.Reader, unlock func(), err error)
	}

	outputProvider interface {
		Reserve(context.Context, execution.Output) (path string, commit, abort func() error, err error)
		Create(context.Context, execution.Output) (w io.Writer, commit, abort func() error, err error)
		Read(context.Context, execution.Output) (r io.Reader, unlock func(), err error)
	}
)
