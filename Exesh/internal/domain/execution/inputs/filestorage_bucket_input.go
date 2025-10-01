package inputs

import (
	"exesh/internal/domain/execution"

	"github.com/DIvanCode/filestorage/pkg/bucket"
)

type FilestorageBucketInput struct {
	execution.InputDetails
	BucketID         bucket.ID `json:"bucket_id"`
	DownloadEndpoint string    `json:"download_endpoint"`
}

func NewFilestorageBucketInput(file string, bucketID bucket.ID, downloadEndpoint string) FilestorageBucketInput {
	return FilestorageBucketInput{
		InputDetails: execution.InputDetails{
			Type: execution.FilestorageBucketInputType,
			File: file,
		},
		BucketID:         bucketID,
		DownloadEndpoint: downloadEndpoint,
	}
}
