package sources

import (
	"taski/internal/domain/testing/source"
)

type InlineSource struct {
	source.Details
	Content string `json:"content"`
}

func NewInlineSource(name source.Name, content string) Source {
	return Source{
		&InlineSource{
			Details: source.Details{
				Type: source.Inline,
				Name: name,
			},
			Content: content,
		},
	}
}
