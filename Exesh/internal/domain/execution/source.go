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
	InputSourceType             SourceType = "input"
	FilestorageBucketSourceType SourceType = "filestorage_bucket"
)

func (s SourceDetails) GetType() SourceType {
	return s.Type
}
