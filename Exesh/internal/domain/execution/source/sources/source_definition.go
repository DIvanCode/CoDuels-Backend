package sources

import (
	"encoding/json"
	"exesh/internal/domain/execution/source"
	"fmt"
)

type Definition struct {
	source.IDefinition
}

func (def *Definition) UnmarshalJSON(data []byte) error {
	var details source.DefinitionDetails
	if err := json.Unmarshal(data, &details); err != nil {
		return fmt.Errorf("failed to unmarshal source definition details: %w", err)
	}

	switch details.Type {
	case source.InlineDefinition:
		def.IDefinition = &InlineSourceDefinition{}
	case source.FilestorageBucketDefinition:
		def.IDefinition = &FilestorageBucketSourceDefinition{}
	case source.FilestorageBucketFileDefinition:
		def.IDefinition = &FilestorageBucketFileSourceDefinition{}
	default:
		return fmt.Errorf("unknown source type: %s", details.Type)
	}

	if err := json.Unmarshal(data, def.IDefinition); err != nil {
		return fmt.Errorf("failed to unmarshal %s source: %w", details.Type, err)
	}

	return nil
}

func (def *Definition) AsInlineDefinition() *InlineSourceDefinition {
	return def.IDefinition.(*InlineSourceDefinition)
}

func (def *Definition) AsFilestorageBucketDefinition() *FilestorageBucketSourceDefinition {
	return def.IDefinition.(*FilestorageBucketSourceDefinition)
}

func (def *Definition) AsFilestorageBucketFileDefinition() *FilestorageBucketFileSourceDefinition {
	return def.IDefinition.(*FilestorageBucketFileSourceDefinition)
}
