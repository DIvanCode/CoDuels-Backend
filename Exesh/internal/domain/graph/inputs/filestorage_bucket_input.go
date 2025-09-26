package inputs

import (
	"exesh/internal/domain/graph"

	"github.com/DIvanCode/filestorage/pkg/bucket"
)

type FilestorageBucketInput struct {
	graph.InputDetails
	BucketID         bucket.ID `json:"bucket_id"`
	DownloadEndpoint string    `json:"download_endpoint"`
	File             string    `json:"file"`
}

func NewFilestorageBucketInput(bucketID bucket.ID, downloadEndpoint string, file string) FilestorageBucketInput {
	return FilestorageBucketInput{
		InputDetails: graph.InputDetails{
			Type: graph.FilestorageBucketInputType,
		},
		BucketID:         bucketID,
		DownloadEndpoint: downloadEndpoint,
		File:             file,
	}
}
