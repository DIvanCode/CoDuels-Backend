package source

type (
	ISource interface {
		GetType() Type
		GetName() Name
	}

	Details struct {
		Type Type `json:"type"`
		Name Name `json:"name"`
	}

	Type string
	Name string
)

const (
	Inline            Type = "inline"
	FilestorageBucket Type = "filestorage_bucket"
)

func (src *Details) GetType() Type {
	return src.Type
}

func (src *Details) GetName() Name {
	return src.Name
}
