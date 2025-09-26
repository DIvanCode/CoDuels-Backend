package providers

import (
	"bytes"
	"context"
	"exesh/internal/domain/graph"
	"exesh/internal/domain/graph/inputs"
	"fmt"
	"io"
)

type InlineInputProvider struct {
}

func NewInlineInputProvider() *InlineInputProvider {
	return &InlineInputProvider{}
}

func (p *InlineInputProvider) SupportsType(inputType graph.InputType) bool {
	return inputType == graph.InlineInputType
}

func (p *InlineInputProvider) Get(ctx context.Context, input graph.Input) (r io.Reader, unlock func(), err error) {
	if input.GetType() != graph.InlineInputType {
		err = fmt.Errorf("unsupported input type %s for %s provider", input.GetType(), graph.InlineInputType)
		return
	}
	typedInput := input.(inputs.InlineInput)

	r = bytes.NewBufferString(typedInput.Content)
	unlock = func() {}

	return
}
