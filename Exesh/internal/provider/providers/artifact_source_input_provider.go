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
	ArtifactInputProvider struct {
		artifactStorage artifactInputStorage
		artifactTTL     time.Duration
	}

	artifactInputStorage interface {
		CreateBucket(bucket.ID, time.Time) (path string, commit, abort func() error, err error)
		CreateFile(bucket.ID, string) (path string, commit, abort func() error, err error)
		DownloadBucket(context.Context, string, bucket.ID, time.Time) error
		GetFile(bucket.ID, string) (path string, unlock func(), err error)
	}
)

func NewArtifactInputProvider(artifactStorage artifactInputStorage, artifactTTL time.Duration) *ArtifactInputProvider {
	return &ArtifactInputProvider{
		artifactStorage: artifactStorage,
		artifactTTL:     artifactTTL,
	}
}

func (p *ArtifactInputProvider) SupportsType(inputType execution.InputType) bool {
	return inputType == execution.ArtifactInputType
}

func (p *ArtifactInputProvider) Create(ctx context.Context, input execution.Input) (w io.Writer, commit, abort func() error, err error) {
	if input.GetType() != execution.ArtifactInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), execution.ArtifactInputType)
		return
	}
	typedInput := input.(inputs.ArtifactInput)

	var bucketID bucket.ID
	if err = bucketID.FromString(typedInput.JobID.String()); err != nil {
		err = fmt.Errorf("failed to create bucket id: %w", err)
		return
	}

	path, commitBucket, abortBucket, err := p.artifactStorage.CreateBucket(bucketID, time.Now().Add(p.artifactTTL))
	if err != nil && errors.Is(err, errs.ErrBucketAlreadyExists) {
		path, commitBucket, abortBucket, err = p.artifactStorage.CreateFile(bucketID, input.GetFile())
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

func (p *ArtifactInputProvider) Locate(ctx context.Context, input execution.Input) (path string, unlock func(), err error) {
	if input.GetType() != execution.ArtifactInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), execution.ArtifactInputType)
		return
	}
	typedInput := input.(inputs.ArtifactInput)

	var bucketID bucket.ID
	if err = bucketID.FromString(typedInput.JobID.String()); err != nil {
		err = fmt.Errorf("failed to convert job id to bucket id: %w", err)
		return
	}

	bucketTrashTime := time.Now().Add(p.artifactTTL)
	if err = p.artifactStorage.DownloadBucket(ctx, typedInput.WorkerID, bucketID, bucketTrashTime); err != nil {
		err = fmt.Errorf("failed to download bucket %s from %s: %w", bucketID.String(), typedInput.WorkerID, err)
		return
	}

	if path, unlock, err = p.artifactStorage.GetFile(bucketID, typedInput.File); err != nil {
		err = fmt.Errorf("failed to get file %s from bucket %s: %w", typedInput.File, bucketID.String(), err)
		return
	}

	return filepath.Join(path, typedInput.File), unlock, nil
}

func (p *ArtifactInputProvider) Read(ctx context.Context, input execution.Input) (r io.Reader, unlock func(), err error) {
	if input.GetType() != execution.ArtifactInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), execution.ArtifactInputType)
		return
	}
	typedInput := input.(inputs.ArtifactInput)

	var bucketID bucket.ID
	if err = bucketID.FromString(typedInput.JobID.String()); err != nil {
		err = fmt.Errorf("failed to convert job id to bucket id: %w", err)
		return
	}

	bucketTrashTime := time.Now().Add(p.artifactTTL)
	if err = p.artifactStorage.DownloadBucket(ctx, typedInput.WorkerID, bucketID, bucketTrashTime); err != nil {
		err = fmt.Errorf("failed to download bucket %s from %s: %w", bucketID.String(), typedInput.WorkerID, err)
		return
	}

	var path string
	if path, unlock, err = p.artifactStorage.GetFile(bucketID, typedInput.File); err != nil {
		err = fmt.Errorf("failed to get file %s from bucket %s: %w", typedInput.File, bucketID.String(), err)
		return
	}

	if r, err = os.OpenFile(filepath.Join(path, typedInput.File), os.O_RDONLY, 0666); err != nil {
		err = fmt.Errorf("failed to open file %s in bucket %s: %w", typedInput.File, bucketID.String(), err)
		return
	}

	return
}
