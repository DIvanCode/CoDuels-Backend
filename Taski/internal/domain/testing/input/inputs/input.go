package inputs

import (
	"encoding/json"
	"fmt"
	"taski/internal/domain/testing/input"
)

type Input struct {
	input.IInput
}

func (in Input) MarshalJSON() ([]byte, error) {
	if in.IInput == nil {
		return []byte("null"), nil
	}

	return json.Marshal(in.IInput)
}

func (in *Input) UnmarshalJSON(data []byte) error {
	var details input.Details
	if err := json.Unmarshal(data, &details); err != nil {
		return fmt.Errorf("failed to unmarshal input details: %w", err)
	}

	switch details.Type {
	case input.Artifact:
		in.IInput = &ArtifactInput{}
	case input.Inline:
		in.IInput = &InlineInput{}
	case input.FilestorageBucket:
		in.IInput = &FilestorageBucketInput{}
	default:
		return fmt.Errorf("unknown input type: %s", details.Type)
	}

	if err := json.Unmarshal(data, in.IInput); err != nil {
		return fmt.Errorf("failed to unmarshal %s input: %w", details.Type, err)
	}

	return nil
}
