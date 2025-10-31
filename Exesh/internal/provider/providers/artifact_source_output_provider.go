package providers

import (
	"context"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/outputs"
	"fmt"
	"io"
	"time"

	"github.com/DIvanCode/filestorage/pkg/bucket"
)

type (
	ArtifactOutputProvider struct {
		artifactStorageAdapter artifactOutputStorageAdapter
		artifactTTL            time.Duration
	}

	artifactOutputStorageAdapter interface {
		Reserve(ctx context.Context, bucketID bucket.ID, file string, ttl time.Duration) (
			path string, commit, abort func() error, err error)
		Create(ctx context.Context, bucketID bucket.ID, file string, ttl time.Duration) (
			w io.Writer, commit, abort func() error, err error)
		Read(ctx context.Context, bucketID bucket.ID, file string, ttl time.Duration, downloadEndpoint string) (
			r io.Reader, unlock func(), err error)
	}
)

func NewArtifactOutputProvider(artifactStorageAdapter artifactOutputStorageAdapter, artifactTTL time.Duration) *ArtifactOutputProvider {
	return &ArtifactOutputProvider{
		artifactStorageAdapter: artifactStorageAdapter,
		artifactTTL:            artifactTTL,
	}
}

func (p *ArtifactOutputProvider) SupportsType(outputType execution.OutputType) bool {
	return outputType == execution.ArtifactOutputType
}

func (p *ArtifactOutputProvider) Reserve(ctx context.Context, output execution.Output) (path string, commit, abort func() error, err error) {
	if output.GetType() != execution.ArtifactOutputType {
		err = fmt.Errorf("unsupported output type %s for %s provider", output.GetType(), execution.ArtifactOutputType)
		return
	}
	var typedOutput outputs.ArtifactOutput
	if _, ok := output.(outputs.ArtifactOutput); ok {
		typedOutput = output.(outputs.ArtifactOutput)
	} else {
		typedOutput = *output.(*outputs.ArtifactOutput)
	}

	var bucketID bucket.ID
	bucketID, err = p.getBucket(typedOutput)
	if err != nil {
		return
	}

	return p.artifactStorageAdapter.Reserve(ctx, bucketID, typedOutput.File, p.artifactTTL)
}

func (p *ArtifactOutputProvider) Create(ctx context.Context, output execution.Output) (w io.Writer, commit, abort func() error, err error) {
	if output.GetType() != execution.ArtifactOutputType {
		err = fmt.Errorf("unsupported output type %s for %s provider", output.GetType(), execution.ArtifactOutputType)
		return
	}
	var typedOutput outputs.ArtifactOutput
	if _, ok := output.(outputs.ArtifactOutput); ok {
		typedOutput = output.(outputs.ArtifactOutput)
	} else {
		typedOutput = *output.(*outputs.ArtifactOutput)
	}

	var bucketID bucket.ID
	bucketID, err = p.getBucket(typedOutput)
	if err != nil {
		return
	}

	return p.artifactStorageAdapter.Create(ctx, bucketID, typedOutput.File, p.artifactTTL)
}

func (p *ArtifactOutputProvider) getBucket(output outputs.ArtifactOutput) (bucketID bucket.ID, err error) {
	if err = bucketID.FromString(output.JobID.String()); err != nil {
		err = fmt.Errorf("failed to convert job id to bucket id: %w", err)
		return
	}
	return
}

func (p *ArtifactOutputProvider) Read(ctx context.Context, output execution.Output) (r io.Reader, unlock func(), err error) {
	if output.GetType() != execution.ArtifactOutputType {
		err = fmt.Errorf("unsupported output type %s for %s provider", output.GetType(), execution.ArtifactOutputType)
		return
	}
	var typedOutput outputs.ArtifactOutput
	if _, ok := output.(outputs.ArtifactOutput); ok {
		typedOutput = output.(outputs.ArtifactOutput)
	} else {
		typedOutput = *output.(*outputs.ArtifactOutput)
	}

	var bucketID bucket.ID
	bucketID, err = p.getBucket(typedOutput)
	if err != nil {
		return
	}

	return p.artifactStorageAdapter.Read(ctx, bucketID, typedOutput.File, p.artifactTTL, "")
}
