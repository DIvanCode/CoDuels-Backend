package executors

import (
	"context"
	"exesh/internal/domain/execution"
	"io"
)

type (
	inputProvider interface {
		Create(context.Context, execution.Input) (w io.Writer, commit, abort func() error, err error)
		Locate(context.Context, execution.Input) (path string, unlock func(), err error)
		Read(context.Context, execution.Input) (r io.Reader, unlock func(), err error)
	}

	outputProvider interface {
		Create(context.Context, execution.Output) (w io.Writer, commit, abort func() error, err error)
		Locate(context.Context, execution.Output) (path string, unlock func(), err error)
		Read(context.Context, execution.Output) (r io.Reader, unlock func(), err error)
	}
)
