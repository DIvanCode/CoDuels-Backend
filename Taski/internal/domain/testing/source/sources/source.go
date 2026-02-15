package sources

import (
	"encoding/json"
	"fmt"
	"taski/internal/domain/testing/source"
)

type (
	Source struct {
		source.ISource
	}

	Sources []Source
)

func (src Source) MarshalJSON() ([]byte, error) {
	if src.ISource == nil {
		return []byte("null"), nil
	}

	return json.Marshal(src.ISource)
}

func (src *Source) UnmarshalJSON(data []byte) error {
	var details source.Details
	if err := json.Unmarshal(data, &details); err != nil {
		return fmt.Errorf("failed to unmarshal source details: %w", err)
	}

	switch details.Type {
	case source.Inline:
		src.ISource = &InlineSource{}
	case source.FilestorageBucket:
		src.ISource = &FilestorageBucketSource{}
	default:
		return fmt.Errorf("unknown source type: %s", details.Type)
	}

	if err := json.Unmarshal(data, src.ISource); err != nil {
		return fmt.Errorf("failed to unmarshal %s source: %w", details.Type, err)
	}

	return nil
}
