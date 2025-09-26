package inputs

import (
	"exesh/internal/domain/execution"

	"github.com/DIvanCode/filestorage/pkg/bucket"
)

type FilestorageBucketInput struct {
	execution.InputDetails
	BucketID         bucket.ID `json:"bucket_id"`
	DownloadEndpoint string    `json:"download_endpoint"`
	File             string    `json:"file"`
}
