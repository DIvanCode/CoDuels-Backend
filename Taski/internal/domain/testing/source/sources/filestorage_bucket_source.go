package sources

import (
	"taski/internal/domain/testing/source"

	"github.com/DIvanCode/filestorage/pkg/bucket"
)

type FilestorageBucketSource struct {
	source.Details
	BucketID         bucket.ID `json:"bucket_id"`
	DownloadEndpoint string    `json:"download_endpoint"`
}

func NewFilestorageBucketSource(name source.Name, bucketID bucket.ID, downloadEndpoint string) Source {
	return Source{
		&FilestorageBucketSource{
			Details: source.Details{
				Type: source.FilestorageBucket,
				Name: name,
			},
			BucketID:         bucketID,
			DownloadEndpoint: downloadEndpoint,
		},
	}
}
