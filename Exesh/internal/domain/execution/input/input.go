package input

import "exesh/internal/domain/execution/source"

type (
	Input struct {
		Type     Type      `json:"type"`
		SourceID source.ID `json:"source"`
	}

	Type string
)

func NewInput(inputType Type, sourceID source.ID) Input {
	return Input{
		Type:     inputType,
		SourceID: sourceID,
	}
}

const (
	Artifact              Type = "artifact"
	Inline                Type = "inline"
	FilestorageBucketFile Type = "filestorage_bucket_file"
)
