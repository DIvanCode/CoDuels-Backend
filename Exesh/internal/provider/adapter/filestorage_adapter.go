package adapter

import (
	"context"
	"errors"
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
		ReserveBucket(context.Context, bucket.ID, *time.Duration) (path string, commit, abort func() error, err error)
		ReserveFile(context.Context, bucket.ID, string) (path string, commit, abort func() error, err error)
		DownloadBucket(context.Context, string, bucket.ID, *time.Duration) error
		DownloadFile(context.Context, string, bucket.ID, string) error
		GetFile(context.Context, bucket.ID, string, *time.Duration) (path string, unlock func(), err error)
	}
)

func NewFilestorageAdapter(filestorage filestorage) *FilestorageAdapter {
	return &FilestorageAdapter{
		filestorage: filestorage,
	}
}

// DownloadBucket
// the bucket will be downloaded
// if the bucket already exists, the ttl will be extended
func (a *FilestorageAdapter) DownloadBucket(
	ctx context.Context,
	bucketID bucket.ID,
	ttl time.Duration,
	downloadEndpoint string,
) error {
	return a.filestorage.DownloadBucket(ctx, downloadEndpoint, bucketID, &ttl)
}

// DownloadFile
// the file will be downloaded
// if the bucket does not exist, it will be created with ttl
// if the file already exists, nothing will happen
func (a *FilestorageAdapter) DownloadFile(
	ctx context.Context,
	bucketID bucket.ID,
	file string,
	ttl time.Duration,
	downloadEndpoint string,
) error {
	err := a.filestorage.DownloadFile(ctx, downloadEndpoint, bucketID, file)
	if err == nil {
		return nil
	}

	// the bucket does not exist, so create it
	_, commit, _, err := a.filestorage.ReserveBucket(ctx, bucketID, &ttl)
	if err != nil {
		return fmt.Errorf("failed to reserve bucket: %w", err)
	}
	if err := commit(); err != nil {
		return fmt.Errorf("failed to commit bucket: %w", err)
	}

	return a.filestorage.DownloadFile(ctx, downloadEndpoint, bucketID, file)
}

// ReserveFile
// the file will be reserved in bucket
// it needs for producing files
// if the bucket does not exist, it will be created
// if the file already exists, then the ErrSourceAlreadyExists will be returned
func (a *FilestorageAdapter) ReserveFile(
	ctx context.Context,
	bucketID bucket.ID,
	file string,
	ttl time.Duration,
) (path string, commit, abort func() error, err error) {
	var commitArtifact, abortArtifact func() error
	path, commitArtifact, abortArtifact, err = a.filestorage.ReserveBucket(ctx, bucketID, &ttl)
	if err != nil && errors.Is(err, errs.ErrBucketAlreadyExists) {
		path, commitArtifact, abortArtifact, err = a.filestorage.ReserveFile(ctx, bucketID, file)
		if err != nil && errors.Is(err, errs.ErrFileAlreadyExists) {
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

// CreateFile
// the file will be reserved in bucket
// it needs for producing files using writer
// if the bucket does not exist, it will be created
// if the file already exists, then the error from filestorage will be returned
func (a *FilestorageAdapter) CreateFile(
	ctx context.Context,
	bucketID bucket.ID,
	file string,
	ttl time.Duration,
) (w io.Writer, commit, abort func() error, err error) {
	path, commitReserve, abortReserve, err := a.ReserveFile(ctx, bucketID, file, ttl)
	if err != nil {
		err = fmt.Errorf("failed to reserve file: %w", err)
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

// LocateFile
// returns real file path
// if the file does not exist, the error will be returned
func (a *FilestorageAdapter) LocateFile(
	ctx context.Context,
	bucketID bucket.ID,
	file string,
	extendTTL time.Duration,
) (path string, unlock func(), err error) {
	path, unlock, err = a.filestorage.GetFile(ctx, bucketID, file, &extendTTL)
	return filepath.Join(path, file), unlock, err
}

// ReadFile
// returns reader to file
// if the file does not exist, the error will be returned
func (a *FilestorageAdapter) ReadFile(
	ctx context.Context,
	bucketID bucket.ID,
	file string,
	extendTTL time.Duration,
) (r io.Reader, unlock func(), err error) {
	path, unlock, err := a.LocateFile(ctx, bucketID, file, extendTTL)
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
