package sources

import (
	"exesh/internal/domain/execution/source"

	"github.com/DIvanCode/filestorage/pkg/bucket"
)

type FilestorageBucketFileSource struct {
	source.Details
	BucketID         bucket.ID `json:"bucket_id"`
	DownloadEndpoint string    `json:"download_endpoint"`
	File             string    `json:"file"`
}

func NewFilestorageBucketFileSource(id source.ID, bucketID bucket.ID, downloadEndpoint string, file string) Source {
	return Source{
		&FilestorageBucketFileSource{
			Details: source.Details{
				ID:   id,
				Type: source.FilestorageBucketFile,
			},
			BucketID:         bucketID,
			DownloadEndpoint: downloadEndpoint,
			File:             file,
		},
	}
}
