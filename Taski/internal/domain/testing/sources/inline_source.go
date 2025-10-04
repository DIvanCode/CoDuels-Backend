package sources

import (
	"taski/internal/domain/testing"
)

type InlineSource struct {
	testing.SourceDetails
	Content string `json:"content"`
}

func NewInlineSource(content string) InlineSource {
	return InlineSource{
		SourceDetails: testing.SourceDetails{
			Type: testing.InlineSource,
		},
		Content: content,
	}
}
