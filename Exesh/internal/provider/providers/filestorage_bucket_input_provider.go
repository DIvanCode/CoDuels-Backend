package providers

import (
	"context"
	"errors"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/inputs"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"time"

	"github.com/DIvanCode/filestorage/pkg/bucket"
	errs "github.com/DIvanCode/filestorage/pkg/errors"
)

type (
	FilestorageBucketInputProvider struct {
		filestorage          filestorage
		filestorageBucketTTL time.Duration
	}

	filestorage interface {
		CreateBucket(bucket.ID, time.Time) (path string, commit, abort func() error, err error)
		CreateFile(bucket.ID, string) (path string, commit, abort func() error, err error)
		DownloadBucket(context.Context, string, bucket.ID, time.Time) error
		GetFile(bucket.ID, string) (path string, unlock func(), err error)
	}
)

func NewFilestorageBucketInputProvider(filestorage filestorage, filestorageBucketTTL time.Duration) *FilestorageBucketInputProvider {
	return &FilestorageBucketInputProvider{
		filestorage:          filestorage,
		filestorageBucketTTL: filestorageBucketTTL,
	}
}

func (p *FilestorageBucketInputProvider) SupportsType(inputType execution.InputType) bool {
	return inputType == execution.FilestorageBucketInputType
}

func (p *FilestorageBucketInputProvider) Create(ctx context.Context, input execution.Input) (w io.Writer, commit, abort func() error, err error) {
	if input.GetType() != execution.FilestorageBucketInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), execution.FilestorageBucketInputType)
		return
	}
	typedInput := input.(inputs.FilestorageBucketInput)

	path, commitBucket, abortBucket, err := p.filestorage.CreateBucket(typedInput.BucketID, time.Now().Add(p.filestorageBucketTTL))
	if err != nil && errors.Is(err, errs.ErrBucketAlreadyExists) {
		path, commitBucket, abortBucket, err = p.filestorage.CreateFile(typedInput.BucketID, input.GetFile())
	}
	if err != nil {
		return nil, nil, nil, fmt.Errorf("failed to create bucket or file: %w", err)
	}

	var f *os.File
	f, err = os.OpenFile(filepath.Join(path, typedInput.File), os.O_CREATE|os.O_WRONLY, 0666)
	if err != nil {
		_ = abortBucket()
		return nil, nil, nil, fmt.Errorf("failed to create file: %w", err)
	}

	commit = func() error {
		_ = f.Close()
		return commitBucket()
	}

	abort = func() error {
		_ = f.Close()
		return abortBucket()
	}

	w = f

	return
}

func (p *FilestorageBucketInputProvider) Locate(ctx context.Context, input execution.Input) (path string, unlock func(), err error) {
	if input.GetType() != execution.FilestorageBucketInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), execution.FilestorageBucketInputType)
		return
	}
	typedInput := input.(inputs.FilestorageBucketInput)

	bucketTrashTime := time.Now().Add(p.filestorageBucketTTL)
	if err = p.filestorage.DownloadBucket(ctx, typedInput.DownloadEndpoint, typedInput.BucketID, bucketTrashTime); err != nil {
		err = fmt.Errorf("failed to download bucket %s from %s: %w", typedInput.BucketID.String(), typedInput.DownloadEndpoint, err)
		return
	}

	if path, unlock, err = p.filestorage.GetFile(typedInput.BucketID, typedInput.File); err != nil {
		err = fmt.Errorf("failed to get file %s from bucket %s: %w", typedInput.File, typedInput.BucketID.String(), err)
		return
	}

	return filepath.Join(path, typedInput.File), unlock, nil
}

func (p *FilestorageBucketInputProvider) Read(ctx context.Context, input execution.Input) (r io.Reader, unlock func(), err error) {
	if input.GetType() != execution.FilestorageBucketInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), execution.FilestorageBucketInputType)
		return
	}
	typedInput := input.(inputs.FilestorageBucketInput)

	bucketTrashTime := time.Now().Add(p.filestorageBucketTTL)
	if err = p.filestorage.DownloadBucket(ctx, typedInput.DownloadEndpoint, typedInput.BucketID, bucketTrashTime); err != nil {
		err = fmt.Errorf("failed to download bucket %s from %s: %w", typedInput.BucketID.String(), typedInput.DownloadEndpoint, err)
		return
	}

	var path string
	if path, unlock, err = p.filestorage.GetFile(typedInput.BucketID, typedInput.File); err != nil {
		err = fmt.Errorf("failed to get file %s from bucket %s: %w", typedInput.File, typedInput.BucketID.String(), err)
		return
	}

	if r, err = os.OpenFile(filepath.Join(path, typedInput.File), os.O_RDONLY, 0666); err != nil {
		err = fmt.Errorf("failed to open file %s in bucket %s: %w", typedInput.File, typedInput.BucketID.String(), err)
		return
	}

	return
}
