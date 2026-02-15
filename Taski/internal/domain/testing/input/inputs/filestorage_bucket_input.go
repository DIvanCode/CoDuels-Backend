package inputs

import (
	"taski/internal/domain/testing/input"
	"taski/internal/domain/testing/source"
)

type FilestorageBucketInput struct {
	input.Details
	SourceName source.Name `json:"source"`
	File       string      `json:"file"`
}

func NewFilestorageBucketInput(sourceName source.Name, file string) Input {
	return Input{
		&FilestorageBucketInput{
			Details:    input.Details{Type: input.FilestorageBucket},
			SourceName: sourceName,
			File:       file,
		},
	}
}
