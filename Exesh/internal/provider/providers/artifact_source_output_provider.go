package providers

import (
	"context"
	"errors"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/outputs"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"time"

	"github.com/DIvanCode/filestorage/pkg/bucket"
	errs "github.com/DIvanCode/filestorage/pkg/errors"
)

type (
	ArtifactOutputProvider struct {
		artifactStorage artifactOutputStorage
		artifactTTL     time.Duration
	}

	artifactOutputStorage interface {
		CreateBucket(bucket.ID, time.Time) (path string, commit, abort func() error, err error)
		CreateFile(bucket.ID, string) (path string, commit, abort func() error, err error)
		GetFile(bucket.ID, string) (path string, unlock func(), err error)
	}
)

func NewArtifactOutputProvider(artifactStorage artifactOutputStorage, artifactTTL time.Duration) *ArtifactOutputProvider {
	return &ArtifactOutputProvider{
		artifactStorage: artifactStorage,
		artifactTTL:     artifactTTL,
	}
}

func (p *ArtifactOutputProvider) SupportsType(outputType execution.OutputType) bool {
	return outputType == execution.ArtifactOutputType
}

func (p *ArtifactOutputProvider) Create(ctx context.Context, output execution.Output) (w io.Writer, commit, abort func() error, err error) {
	if output.GetType() != execution.ArtifactOutputType {
		err = fmt.Errorf("unsupported output type %s for %s provider", output.GetType(), execution.ArtifactOutputType)
		return
	}
	typedOutput := output.(outputs.ArtifactOutput)

	var bucketID bucket.ID
	if err = bucketID.FromString(typedOutput.JobID.String()); err != nil {
		err = fmt.Errorf("failed to create bucket id: %w", err)
		return
	}

	path, commitBucket, abortBucket, err := p.artifactStorage.CreateBucket(bucketID, time.Now().Add(p.artifactTTL))
	if err != nil && errors.Is(err, errs.ErrBucketAlreadyExists) {
		path, commitBucket, abortBucket, err = p.artifactStorage.CreateFile(bucketID, output.GetFile())
	}
	if err != nil {
		return nil, nil, nil, fmt.Errorf("failed to create bucket or file: %w", err)
	}

	var f *os.File
	f, err = os.OpenFile(filepath.Join(path, typedOutput.File), os.O_CREATE|os.O_WRONLY, 0666)
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

func (p *ArtifactOutputProvider) Locate(ctx context.Context, output execution.Output) (path string, unlock func(), err error) {
	if output.GetType() != execution.ArtifactOutputType {
		err = fmt.Errorf("unsupported output type %s for %s provider", output.GetType(), execution.ArtifactOutputType)
		return
	}
	typedOutput := output.(outputs.ArtifactOutput)

	var bucketID bucket.ID
	if err = bucketID.FromString(typedOutput.JobID.String()); err != nil {
		err = fmt.Errorf("failed to convert job id to bucket id: %w", err)
		return
	}

	if path, unlock, err = p.artifactStorage.GetFile(bucketID, typedOutput.File); err != nil {
		err = fmt.Errorf("failed to get file %s from bucket %s: %w", typedOutput.File, bucketID.String(), err)
		return
	}

	return filepath.Join(path, typedOutput.File), unlock, nil
}

func (p *ArtifactOutputProvider) Read(ctx context.Context, output execution.Output) (r io.Reader, unlock func(), err error) {
	if output.GetType() != execution.ArtifactOutputType {
		err = fmt.Errorf("unsupported output type %s for %s provider", output.GetType(), execution.ArtifactOutputType)
		return
	}
	typedOutput := output.(outputs.ArtifactOutput)

	var bucketID bucket.ID
	if err = bucketID.FromString(typedOutput.JobID.String()); err != nil {
		err = fmt.Errorf("failed to convert job id to bucket id: %w", err)
		return
	}

	var path string
	if path, unlock, err = p.artifactStorage.GetFile(bucketID, typedOutput.File); err != nil {
		err = fmt.Errorf("failed to get file %s from bucket %s: %w", typedOutput.File, bucketID.String(), err)
		return
	}

	if r, err = os.OpenFile(filepath.Join(path, typedOutput.File), os.O_RDONLY, 0666); err != nil {
		err = fmt.Errorf("failed to open file %s in bucket %s: %w", typedOutput.File, bucketID.String(), err)
		return
	}

	return
}
