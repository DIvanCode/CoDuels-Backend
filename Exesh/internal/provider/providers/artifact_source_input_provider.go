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
	ArtifactInputProvider struct {
		artifactStorageAdapter artifactInputStorageAdapter
		artifactTTL            time.Duration
	}

	artifactInputStorageAdapter interface {
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

func NewArtifactInputProvider(artifactStorageAdapter artifactInputStorageAdapter, artifactTTL time.Duration) *ArtifactInputProvider {
	return &ArtifactInputProvider{
		artifactStorageAdapter: artifactStorageAdapter,
		artifactTTL:            artifactTTL,
	}
}

func (p *ArtifactInputProvider) SupportsType(inputType execution.InputType) bool {
	return inputType == execution.ArtifactInputType
}

func (p *ArtifactInputProvider) Reserve(ctx context.Context, input execution.Input) (path string, commit, abort func() error, err error) {
	if input.GetType() != execution.ArtifactInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), execution.ArtifactInputType)
		return
	}
	var typedInput inputs.ArtifactInput
	if _, ok := input.(inputs.ArtifactInput); ok {
		typedInput = input.(inputs.ArtifactInput)
	} else {
		typedInput = *input.(*inputs.ArtifactInput)
	}

	var bucketID bucket.ID
	bucketID, err = p.getBucket(typedInput)
	if err != nil {
		return
	}

	return p.artifactStorageAdapter.Reserve(ctx, bucketID, typedInput.File, p.artifactTTL)
}

func (p *ArtifactInputProvider) Create(ctx context.Context, input execution.Input) (w io.Writer, commit, abort func() error, err error) {
	if input.GetType() != execution.ArtifactInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), execution.ArtifactInputType)
		return
	}
	var typedInput inputs.ArtifactInput
	if _, ok := input.(inputs.ArtifactInput); ok {
		typedInput = input.(inputs.ArtifactInput)
	} else {
		typedInput = *input.(*inputs.ArtifactInput)
	}

	var bucketID bucket.ID
	bucketID, err = p.getBucket(typedInput)
	if err != nil {
		return
	}

	return p.artifactStorageAdapter.Create(ctx, bucketID, typedInput.File, p.artifactTTL)
}

func (p *ArtifactInputProvider) Locate(ctx context.Context, input execution.Input) (path string, unlock func(), err error) {
	if input.GetType() != execution.ArtifactInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), execution.ArtifactInputType)
		return
	}
	var typedInput inputs.ArtifactInput
	if _, ok := input.(inputs.ArtifactInput); ok {
		typedInput = input.(inputs.ArtifactInput)
	} else {
		typedInput = *input.(*inputs.ArtifactInput)
	}

	var bucketID bucket.ID
	bucketID, err = p.getBucket(typedInput)
	if err != nil {
		return
	}

	return p.artifactStorageAdapter.Locate(ctx, bucketID, typedInput.File, p.artifactTTL, typedInput.WorkerID)
}

func (p *ArtifactInputProvider) Read(ctx context.Context, input execution.Input) (r io.Reader, unlock func(), err error) {
	if input.GetType() != execution.ArtifactInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), execution.ArtifactInputType)
		return
	}
	var typedInput inputs.ArtifactInput
	if _, ok := input.(inputs.ArtifactInput); ok {
		typedInput = input.(inputs.ArtifactInput)
	} else {
		typedInput = *input.(*inputs.ArtifactInput)
	}

	var bucketID bucket.ID
	bucketID, err = p.getBucket(typedInput)
	if err != nil {
		return
	}

	return p.artifactStorageAdapter.Read(ctx, bucketID, typedInput.File, p.artifactTTL, typedInput.WorkerID)
}

func (p *ArtifactInputProvider) getBucket(input inputs.ArtifactInput) (bucketID bucket.ID, err error) {
	if err = bucketID.FromString(input.JobID.String()); err != nil {
		err = fmt.Errorf("failed to convert job id to bucket id: %w", err)
		return
	}
	return
}
