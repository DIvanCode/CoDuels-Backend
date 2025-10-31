package providers

import (
	"context"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/inputs"
	"fmt"
	"io"
	"time"

	"github.com/DIvanCode/filestorage/pkg/bucket"
)

type (
	FilestorageBucketInputProvider struct {
		filestorageAdapter filestorageAdapter
		artifactTTL        time.Duration
	}

	filestorageAdapter interface {
		Reserve(ctx context.Context, bucketID bucket.ID, file string, ttl time.Duration) (
			path string, commit, abort func() error, err error)
		Create(ctx context.Context, bucketID bucket.ID, file string, ttl time.Duration) (
			w io.Writer, commit, abort func() error, err error)
		Locate(ctx context.Context, bucketID bucket.ID, file string, ttl time.Duration, downloadEndpoint string) (
			path string, unlock func(), err error)
		Read(ctx context.Context, bucketID bucket.ID, file string, ttl time.Duration, downloadEndpoint string) (
			r io.Reader, unlock func(), err error)
	}
)

func NewFilestorageBucketInputProvider(filestorageAdapter filestorageAdapter, artifactTTL time.Duration) *FilestorageBucketInputProvider {
	return &FilestorageBucketInputProvider{
		filestorageAdapter: filestorageAdapter,
		artifactTTL:        artifactTTL,
	}
}

func (p *FilestorageBucketInputProvider) SupportsType(inputType execution.InputType) bool {
	return inputType == execution.FilestorageBucketInputType
}

func (p *FilestorageBucketInputProvider) Reserve(ctx context.Context, input execution.Input) (path string, commit, abort func() error, err error) {
	if input.GetType() != execution.FilestorageBucketInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), execution.FilestorageBucketInputType)
		return
	}
	var typedInput inputs.FilestorageBucketInput
	if _, ok := input.(inputs.FilestorageBucketInput); ok {
		typedInput = input.(inputs.FilestorageBucketInput)
	} else {
		typedInput = *input.(*inputs.FilestorageBucketInput)
	}

	return p.filestorageAdapter.Reserve(ctx, typedInput.BucketID, typedInput.File, p.artifactTTL)
}

func (p *FilestorageBucketInputProvider) Create(ctx context.Context, input execution.Input) (w io.Writer, commit, abort func() error, err error) {
	if input.GetType() != execution.FilestorageBucketInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), execution.FilestorageBucketInputType)
		return
	}
	var typedInput inputs.FilestorageBucketInput
	if _, ok := input.(inputs.FilestorageBucketInput); ok {
		typedInput = input.(inputs.FilestorageBucketInput)
	} else {
		typedInput = *input.(*inputs.FilestorageBucketInput)
	}

	return p.filestorageAdapter.Create(ctx, typedInput.BucketID, typedInput.File, p.artifactTTL)
}

func (p *FilestorageBucketInputProvider) Locate(ctx context.Context, input execution.Input) (path string, unlock func(), err error) {
	if input.GetType() != execution.FilestorageBucketInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), execution.FilestorageBucketInputType)
		return
	}
	var typedInput inputs.FilestorageBucketInput
	if _, ok := input.(inputs.FilestorageBucketInput); ok {
		typedInput = input.(inputs.FilestorageBucketInput)
	} else {
		typedInput = *input.(*inputs.FilestorageBucketInput)
	}

	return p.filestorageAdapter.Locate(ctx, typedInput.BucketID, typedInput.File, p.artifactTTL, typedInput.DownloadEndpoint)
}

func (p *FilestorageBucketInputProvider) Read(ctx context.Context, input execution.Input) (r io.Reader, unlock func(), err error) {
	if input.GetType() != execution.FilestorageBucketInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), execution.FilestorageBucketInputType)
		return
	}
	var typedInput inputs.FilestorageBucketInput
	if _, ok := input.(inputs.FilestorageBucketInput); ok {
		typedInput = input.(inputs.FilestorageBucketInput)
	} else {
		typedInput = *input.(*inputs.FilestorageBucketInput)
	}

	return p.filestorageAdapter.Read(ctx, typedInput.BucketID, typedInput.File, p.artifactTTL, typedInput.DownloadEndpoint)
}
