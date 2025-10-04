package sources

import (
	"taski/internal/domain/testing"

	"github.com/DIvanCode/filestorage/pkg/bucket"
)

type FilestorageBucketSource struct {
	testing.SourceDetails
	BucketID         bucket.ID `json:"bucket_id"`
	DownloadEndpoint string    `json:"download_endpoint"`
	File             string    `json:"file"`
}

func NewFilestorageBucketSource(bucketID bucket.ID, downloadEndpoint string, file string) FilestorageBucketSource {
	return FilestorageBucketSource{
		SourceDetails: testing.SourceDetails{
			Type: testing.FilestorageBucketSource,
		},
		BucketID:         bucketID,
		DownloadEndpoint: downloadEndpoint,
		File:             file,
	}
}
