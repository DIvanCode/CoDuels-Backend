package source

type (
	IDefinition interface {
		GetType() DefinitionType
		GetName() DefinitionName
	}

	DefinitionDetails struct {
		Type DefinitionType `json:"type"`
		Name DefinitionName `json:"name"`
	}

	DefinitionType string
	DefinitionName string
)

const (
	InlineDefinition                DefinitionType = "inline"
	FilestorageBucketDefinition     DefinitionType = "filestorage_bucket"
	FilestorageBucketFileDefinition DefinitionType = "filestorage_bucket_file"
)

func (def *DefinitionDetails) GetType() DefinitionType {
	return def.Type
}

func (def *DefinitionDetails) GetName() DefinitionName {
	return def.Name
}
