package execution

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
	OtherStepInputType         InputType = "other_step"
	InlineInputType            InputType = "inline"
	FilestorageBucketInputType InputType = "filestorage_bucket"
)

func (input InputDetails) GetType() InputType {
	return input.Type
}
