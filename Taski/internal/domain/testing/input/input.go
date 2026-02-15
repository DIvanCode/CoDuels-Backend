package input

type (
	IInput interface {
		GetType() Type
	}

	Details struct {
		Type Type `json:"type"`
	}

	Type string
)

const (
	Artifact          Type = "artifact"
	Inline            Type = "inline"
	FilestorageBucket Type = "filestorage_bucket"
)

func (src *Details) GetType() Type {
	return src.Type
}
