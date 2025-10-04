package testing

type (
	Source interface {
		GetType() SourceType
	}

	SourceDetails struct {
		Type SourceType `json:"type"`
	}

	SourceType string
)

const (
	OtherStepSource         SourceType = "other_step"
	InlineSource            SourceType = "inline"
	FilestorageBucketSource SourceType = "filestorage_bucket"
)

func (s SourceDetails) GetType() SourceType {
	return s.Type
}
