package executors

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/source"
	"io"
	"time"
)

type (
	sourceProvider interface {
		Locate(context.Context, source.ID) (path string, unlock func(), err error)
	}

	outputProvider interface {
		Reserve(context.Context, job.ID, string) (path string, commit, abort func() (*time.Time, error), err error)
		Read(context.Context, job.ID, string) (r io.Reader, unlock func(), err error)
	}
)
