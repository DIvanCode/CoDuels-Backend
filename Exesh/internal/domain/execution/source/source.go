package source

type (
	ISource interface {
		GetType() Type
		GetID() ID
	}

	Details struct {
		Type Type `json:"type"`
		ID   ID   `json:"id"`
	}

	Type string
)

const (
	Inline                Type = "inline"
	FilestorageBucketFile Type = "filestorage_bucket_file"
)

func (src *Details) GetType() Type {
	return src.Type
}

func (src *Details) GetID() ID {
	return src.ID
}
