package inputs

import (
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/source"
)

type FilestorageBucketFileInputDefinition struct {
	input.DefinitionDetails
	SourceDefinitionName source.DefinitionName `json:"source"`
}
