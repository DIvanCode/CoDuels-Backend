package executors

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/source"
	"io"
)

type (
	sourceProvider interface {
		Locate(context.Context, source.ID) (path string, unlock func(), err error)
		Read(context.Context, source.ID) (r io.Reader, unlock func(), err error)
	}

	outputProvider interface {
		Reserve(context.Context, job.ID, string) (path string, commit, abort func() error, err error)
		Read(context.Context, job.ID, string) (r io.Reader, unlock func(), err error)
		Create(context.Context, job.ID, string) (w io.Writer, commit, abort func() error, err error)
	}
)
