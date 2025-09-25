package sources

import (
	"exesh/internal/domain/execution"

	"github.com/DIvanCode/filestorage/pkg/bucket"
)

type FilestorageBucketSource struct {
	execution.SourceDetails
	Name             string    `json:"name"`
	BucketID         bucket.ID `json:"bucket_id"`
	DownloadEndpoint string    `json:"download_endpoint"`
	File             string    `json:"file"`
}
