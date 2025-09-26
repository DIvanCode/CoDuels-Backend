package inputs

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"fmt"
)

func UnmarshalInputJSON(data []byte) (input execution.Input, err error) {
	var details execution.InputDetails
	if err = json.Unmarshal(data, &details); err != nil {
		err = fmt.Errorf("failed to unmarshal input details: %w", err)
		return
	}

	switch details.Type {
	case execution.OtherStepInputType:
		input = &OtherStepInput{}
	case execution.InlineInputType:
		input = &InlineInput{}
	case execution.FilestorageBucketInputType:
		input = &FilestorageBucketInput{}
	default:
		err = fmt.Errorf("unknown input type: %s", details.Type)
		return
	}

	if err = json.Unmarshal(data, input); err != nil {
		err = fmt.Errorf("failed to unmarshal %s input: %w", details.Type, err)
		return
	}
	return
}
