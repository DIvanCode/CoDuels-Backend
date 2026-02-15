package inputs

import (
	"taski/internal/domain/testing/input"
	"taski/internal/domain/testing/source"
)

type InlineInput struct {
	input.Details
	SourceName source.Name `json:"source"`
}

func NewInlineInput(sourceName source.Name) Input {
	return Input{
		&InlineInput{
			Details:    input.Details{Type: input.Inline},
			SourceName: sourceName,
		},
	}
}
