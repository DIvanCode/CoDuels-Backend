package providers

import (
	"context"
	"exesh/internal/domain/graph"
	"exesh/internal/domain/graph/inputs"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"time"

	"github.com/DIvanCode/filestorage/pkg/bucket"
)

type (
	FilestorageBucketInputProvider struct {
		filestorage          filestorage
		filestorageBucketTTL time.Duration
	}

	filestorage interface {
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

func (p *FilestorageBucketInputProvider) SupportsType(inputType graph.InputType) bool {
	return inputType == graph.FilestorageBucketInputType
}

func (p *FilestorageBucketInputProvider) Get(ctx context.Context, input graph.Input) (r io.Reader, unlock func(), err error) {
	if input.GetType() != graph.FilestorageBucketInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), graph.FilestorageBucketInputType)
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
