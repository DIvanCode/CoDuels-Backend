package outputs

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"fmt"
)

func UnmarshalOutputJSON(data []byte) (output execution.Output, err error) {
	var details execution.OutputDetails
	if err = json.Unmarshal(data, &details); err != nil {
		err = fmt.Errorf("failed to unmarshal output details: %w", err)
		return
	}

	switch details.Type {
	case execution.ArtifactOutputType:
		output = &ArtifactOutput{}
	default:
		err = fmt.Errorf("unknown output type: %s", details.Type)
		return
	}

	if err = json.Unmarshal(data, output); err != nil {
		err = fmt.Errorf("failed to unmarshal %s output: %w", details.Type, err)
		return
	}
	return
}
