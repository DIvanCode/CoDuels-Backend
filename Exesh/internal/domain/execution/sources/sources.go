package sources

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"fmt"
)

func UnmarshalSourceJSON(data []byte) (Source execution.Source, err error) {
	var details execution.SourceDetails
	if err = json.Unmarshal(data, &details); err != nil {
		err = fmt.Errorf("failed to unmarshal source details: %w", err)
		return
	}

	switch details.Type {
	case execution.OtherStepSourceType:
		Source = &OtherStepSource{}
	case execution.InlineSourceType:
		Source = &InlineSource{}
	case execution.FilestorageBucketSourceType:
		Source = &FilestorageBucketSource{}
	default:
		err = fmt.Errorf("unknown source type: %s", details.Type)
		return
	}

	if err = json.Unmarshal(data, Source); err != nil {
		err = fmt.Errorf("failed to unmarshal %s source: %w", details.Type, err)
		return
	}
	return
}
