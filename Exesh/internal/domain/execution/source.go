package execution

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
	OtherStepSourceType         SourceType = "other_step"
	InlineSourceType            SourceType = "inline"
	FilestorageBucketSourceType SourceType = "filestorage_bucket"
)

func (Source SourceDetails) GetType() SourceType {
	return Source.Type
}
