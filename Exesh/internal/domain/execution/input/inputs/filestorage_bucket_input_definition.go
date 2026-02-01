package inputs

import (
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/source"
)

type FilestorageBucketInputDefinition struct {
	input.DefinitionDetails
	SourceDefinitionName source.DefinitionName `json:"source"`
	File                 string                `json:"file"`
}
