package execution

type (
	Input interface {
		GetType() InputType
		GetFile() string
	}

	InputDetails struct {
		Type InputType `json:"type"`
		File string    `json:"file"`
	}

	InputType string
)

const (
	ArtifactInputType          InputType = "artifact"
	FilestorageBucketInputType InputType = "filestorage_bucket"
)

func (input InputDetails) GetType() InputType {
	return input.Type
}

func (input InputDetails) GetFile() string {
	return input.File
}
