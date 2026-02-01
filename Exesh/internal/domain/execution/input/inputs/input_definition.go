package inputs

import (
	"encoding/json"
	"exesh/internal/domain/execution/input"
	"fmt"
)

type Definition struct {
	input.IDefinition
}

func (def *Definition) UnmarshalJSON(data []byte) error {
	var details input.DefinitionDetails
	if err := json.Unmarshal(data, &details); err != nil {
		return fmt.Errorf("failed to unmarshal input definition details: %w", err)
	}

	switch details.Type {
	case input.ArtifactDefinition:
		def.IDefinition = &ArtifactInputDefinition{}
	case input.InlineDefinition:
		def.IDefinition = &InlineInputDefinition{}
	case input.FilestorageBucketDefinition:
		def.IDefinition = &FilestorageBucketInputDefinition{}
	case input.FilestorageBucketFileDefinition:
		def.IDefinition = &FilestorageBucketFileInputDefinition{}
	default:
		return fmt.Errorf("unknown input definition type: %s", details.Type)
	}

	if err := json.Unmarshal(data, def.IDefinition); err != nil {
		return fmt.Errorf("failed to unmarshal %s input definition: %w", details.Type, err)
	}

	return nil
}

func (def *Definition) AsArtifact() *ArtifactInputDefinition {
	return def.IDefinition.(*ArtifactInputDefinition)
}

func (def *Definition) AsInline() *InlineInputDefinition {
	return def.IDefinition.(*InlineInputDefinition)
}

func (def *Definition) AsFilestorageBucket() *FilestorageBucketInputDefinition {
	return def.IDefinition.(*FilestorageBucketInputDefinition)
}

func (def *Definition) AsFilestorageBucketFile() *FilestorageBucketFileInputDefinition {
	return def.IDefinition.(*FilestorageBucketFileInputDefinition)
}
