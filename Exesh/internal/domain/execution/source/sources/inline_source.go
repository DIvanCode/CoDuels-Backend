package sources

import (
	"exesh/internal/domain/execution/source"
)

type InlineSource struct {
	source.Details
	Content string `json:"content"`
}

func NewInlineSource(id source.ID, content string) Source {
	return Source{
		&InlineSource{
			Details: source.Details{
				ID:   id,
				Type: source.Inline,
			},
			Content: content,
		},
	}
}
