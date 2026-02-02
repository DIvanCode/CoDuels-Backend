package sources

import (
	"exesh/internal/domain/execution/source"

	"github.com/DIvanCode/filestorage/pkg/bucket"
)

type FilestorageBucketSourceDefinition struct {
	source.DefinitionDetails
	BucketID         bucket.ID `json:"bucket_id"`
	DownloadEndpoint string    `json:"download_endpoint"`
}
