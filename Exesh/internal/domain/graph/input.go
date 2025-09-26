package graph

type (
	Input interface {
		GetType() InputType
	}

	InputDetails struct {
		Type InputType `json:"type"`
	}

	InputType string
)

const (
	ArtifactInputType          InputType = "artifact"
	InlineInputType            InputType = "inline"
	FilestorageBucketInputType InputType = "filestorage_bucket"
)

func (input InputDetails) GetType() InputType {
	return input.Type
}
