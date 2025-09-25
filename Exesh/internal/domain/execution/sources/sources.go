package sources

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"fmt"
)

func UnmarshalSourceJSON(data []byte) (source execution.Source, err error) {
	var details execution.SourceDetails
	if err = json.Unmarshal(data, &details); err != nil {
		err = fmt.Errorf("failed to unmarshal source details: %w", err)
		return
	}

	switch details.Type {
	case execution.OtherStepSourceType:
		source = &OtherStepSource{}
	case execution.InputSourceType:
		source = &InputSource{}
	case execution.FilestorageBucketSourceType:
		source = &FilestorageBucketSource{}
	default:
		err = fmt.Errorf("unknown source type: %s", details.Type)
		return
	}

	if err = json.Unmarshal(data, source); err != nil {
		err = fmt.Errorf("failed to unmarshal %s source: %w", details.Type, err)
		return
	}
	return
}
