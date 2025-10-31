package adapter

import (
	"context"
	"errors"
	"exesh/internal/provider"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"time"

	"github.com/DIvanCode/filestorage/pkg/bucket"
	errs "github.com/DIvanCode/filestorage/pkg/errors"
)

type (
	FilestorageAdapter struct {
		filestorage filestorage
	}

	filestorage interface {
		ReserveBucket(context.Context, bucket.ID, time.Duration) (path string, commit, abort func() error, err error)
		ReserveFile(context.Context, bucket.ID, string) (path string, commit, abort func() error, err error)
		DownloadBucket(context.Context, string, bucket.ID, time.Duration) error
		DownloadFile(context.Context, string, bucket.ID, string) error
		GetFile(context.Context, bucket.ID, string) (path string, unlock func(), err error)
	}
)

func NewFilestorageAdapter(filestorage filestorage) *FilestorageAdapter {
	return &FilestorageAdapter{
		filestorage: filestorage,
	}
}

func (a *FilestorageAdapter) Reserve(
	ctx context.Context,
	bucketID bucket.ID,
	file string,
	ttl time.Duration,
) (path string, commit, abort func() error, err error) {
	var commitArtifact, abortArtifact func() error
	path, commitArtifact, abortArtifact, err = a.filestorage.ReserveBucket(ctx, bucketID, ttl)
	if err != nil && errors.Is(err, errs.ErrBucketAlreadyExists) {
		path, commitArtifact, abortArtifact, err = a.filestorage.ReserveFile(ctx, bucketID, file)
		if err != nil && errors.Is(err, errs.ErrFileAlreadyExists) {
			err = provider.ErrInputAlreadyExists
			return
		}
	}
	if err != nil {
		err = fmt.Errorf("failed to reserve bucket or file: %w", err)
		return
	}

	commit = func() error {
		if err = commitArtifact(); err != nil {
			_ = abortArtifact()
			return fmt.Errorf("failed to commit artifact: %w", err)
		}
		return nil
	}

	abort = func() error {
		if err = abortArtifact(); err != nil {
			return fmt.Errorf("failed to abort artifact: %w", err)
		}
		return nil
	}

	return filepath.Join(path, file), commit, abort, nil
}

func (a *FilestorageAdapter) Create(
	ctx context.Context,
	bucketID bucket.ID,
	file string,
	ttl time.Duration,
) (w io.Writer, commit, abort func() error, err error) {
	path, commitReserve, abortReserve, err := a.Reserve(ctx, bucketID, file, ttl)
	if err != nil {
		return
	}

	var f *os.File
	f, err = os.OpenFile(path, os.O_CREATE|os.O_TRUNC|os.O_WRONLY, 0666)
	if err != nil {
		_ = abortReserve()
		err = fmt.Errorf("failed to create file: %w", err)
		return
	}

	commit = func() error {
		if err = f.Close(); err != nil {
			_ = abortReserve()
			return fmt.Errorf("failed to close file: %w", err)
		}
		if err = commitReserve(); err != nil {
			_ = abortReserve()
			return err
		}
		return nil
	}

	abort = func() error {
		if err = f.Close(); err != nil {
			_ = abortReserve()
			return fmt.Errorf("failed to close file: %w", err)
		}
		if err = abortReserve(); err != nil {
			return err
		}
		return nil
	}

	w = f

	return
}

func (a *FilestorageAdapter) Locate(
	ctx context.Context,
	bucketID bucket.ID,
	file string,
	ttl time.Duration,
	downloadEndpoint string,
) (path string, unlock func(), err error) {
	if path, unlock, err = a.filestorage.GetFile(ctx, bucketID, file); err != nil {
		if err = a.filestorage.DownloadFile(ctx, downloadEndpoint, bucketID, file); err != nil {
			if !errors.Is(err, errs.ErrBucketNotFound) {
				err = fmt.Errorf("failed to download file: %w", err)
				return
			}

			var commit, abort func() error
			_, commit, abort, err = a.filestorage.ReserveBucket(ctx, bucketID, ttl)
			if err != nil && !errors.Is(err, errs.ErrBucketAlreadyExists) {
				err = fmt.Errorf("failed to reserve bucket: %w", err)
				return
			}
			if err == nil {
				if err = commit(); err != nil {
					_ = abort()
					err = fmt.Errorf("failed to commit bucket: %w", err)
					return
				}
			}

			err = a.filestorage.DownloadFile(ctx, downloadEndpoint, bucketID, file)
		}
		if err != nil {
			err = fmt.Errorf("failed to download file: %w", err)
			return
		}

		path, unlock, err = a.filestorage.GetFile(ctx, bucketID, file)
	}
	if err != nil {
		err = fmt.Errorf("failed to get file from bucket: %w", err)
		return
	}

	return filepath.Join(path, file), unlock, nil
}

func (a *FilestorageAdapter) Read(
	ctx context.Context,
	bucketID bucket.ID,
	file string,
	ttl time.Duration,
	downloadEndpoint string,
) (r io.Reader, unlock func(), err error) {
	path, unlock, err := a.Locate(ctx, bucketID, file, ttl, downloadEndpoint)
	if err != nil {
		return
	}

	if r, err = os.OpenFile(path, os.O_RDONLY, 0666); err != nil {
		unlock()
		err = fmt.Errorf("failed to open file: %w", err)
		return
	}

	return
}
