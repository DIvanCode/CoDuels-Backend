package inputs

import "exesh/internal/domain/graph"

type InlineInput struct {
	graph.InputDetails
	Content string `json:"content"`
}

func NewInlineInput(content string) InlineInput {
	return InlineInput{
		InputDetails: graph.InputDetails{
			Type: graph.InlineInputType,
		},
		Content: content,
	}
}
