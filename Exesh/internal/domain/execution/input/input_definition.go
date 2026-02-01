package input

type (
	IDefinition interface {
		GetType() DefinitionType
	}

	DefinitionDetails struct {
		Type DefinitionType `json:"type"`
	}

	DefinitionType string
)

const (
	ArtifactDefinition              DefinitionType = "artifact"
	InlineDefinition                DefinitionType = "inline"
	FilestorageBucketDefinition     DefinitionType = "filestorage_bucket"
	FilestorageBucketFileDefinition DefinitionType = "filestorage_bucket_file"
)

func (def *DefinitionDetails) GetType() DefinitionType {
	return def.Type
}
