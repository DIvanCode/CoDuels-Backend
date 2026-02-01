package sources

import (
	"encoding/json"
	"exesh/internal/domain/execution/source"
	"fmt"
)

type Source struct {
	source.ISource
}

func (src *Source) UnmarshalJSON(data []byte) error {
	var details source.Details
	if err := json.Unmarshal(data, &details); err != nil {
		return fmt.Errorf("failed to unmarshal source details: %w", err)
	}

	switch details.Type {
	case source.Inline:
		src.ISource = &InlineSource{}
	case source.FilestorageBucketFile:
		src.ISource = &FilestorageBucketFileSource{}
	default:
		return fmt.Errorf("unknown source type: %s", details.Type)
	}

	if err := json.Unmarshal(data, src.ISource); err != nil {
		return fmt.Errorf("failed to unmarshal %s source: %w", details.Type, err)
	}

	return nil
}

func (src *Source) AsInline() *InlineSource {
	return src.ISource.(*InlineSource)
}

func (src *Source) AsFilestorageBucketFile() *FilestorageBucketFileSource {
	return src.ISource.(*FilestorageBucketFileSource)
}
