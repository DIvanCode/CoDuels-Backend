package executors

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/source"
	"exesh/internal/runtime"
	"fmt"
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

// copyInputToRuntime releases each source lock before another source is read.
// Inputs can share a bucket, whose TTL update needs a write lock.
func copyInputToRuntime(ctx context.Context, provider sourceProvider, rt runtime.Runtime, id source.ID, runtimePath string) error {
	path, unlock, err := provider.Locate(ctx, id)
	if err != nil {
		return fmt.Errorf("locate source %s: %w", id.String(), err)
	}
	defer unlock()
	if err := rt.CopyToRuntime(ctx, path, runtimePath); err != nil {
		return fmt.Errorf("copy source %s to runtime: %w", id.String(), err)
	}
	return nil
}
