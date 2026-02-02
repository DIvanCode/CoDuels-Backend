package sources

import (
	"database/sql/driver"
	"encoding/json"
	"exesh/internal/domain/execution/source"
	"fmt"
)

type (
	Definition struct {
		source.IDefinition
	}

	Definitions []Definition
)

func (def Definition) MarshalJSON() ([]byte, error) {
	if def.IDefinition == nil {
		return []byte("null"), nil
	}

	return json.Marshal(def.IDefinition)
}

func (def *Definition) UnmarshalJSON(data []byte) error {
	var details source.DefinitionDetails
	if err := json.Unmarshal(data, &details); err != nil {
		return fmt.Errorf("failed to unmarshal source definition details: %w", err)
	}

	switch details.Type {
	case source.InlineDefinition:
		def.IDefinition = &InlineSourceDefinition{}
	case source.FilestorageBucketDefinition:
		def.IDefinition = &FilestorageBucketSourceDefinition{}
	case source.FilestorageBucketFileDefinition:
		def.IDefinition = &FilestorageBucketFileSourceDefinition{}
	default:
		return fmt.Errorf("unknown source type: %s", details.Type)
	}

	if err := json.Unmarshal(data, def.IDefinition); err != nil {
		return fmt.Errorf("failed to unmarshal %s source: %w", details.Type, err)
	}

	return nil
}

func (defs Definitions) Value() (driver.Value, error) {
	b, err := json.Marshal(defs)
	if err != nil {
		return nil, fmt.Errorf("failed to marshal source definitions: %w", err)
	}
	return b, nil
}

func (defs *Definitions) Scan(src any) error {
	if src == nil {
		*defs = nil
		return nil
	}

	var data []byte
	switch v := src.(type) {
	case []byte:
		data = v
	case string:
		data = []byte(v)
	default:
		return fmt.Errorf("failed to scan source definitions from type %T", src)
	}

	if len(data) == 0 {
		*defs = nil
		return nil
	}

	var out Definitions
	if err := json.Unmarshal(data, &out); err != nil {
		return fmt.Errorf("failed to unmarshal source definitions: %w", err)
	}

	*defs = out
	return nil
}

func (def *Definition) AsInlineDefinition() *InlineSourceDefinition {
	return def.IDefinition.(*InlineSourceDefinition)
}

func (def *Definition) AsFilestorageBucketDefinition() *FilestorageBucketSourceDefinition {
	return def.IDefinition.(*FilestorageBucketSourceDefinition)
}

func (def *Definition) AsFilestorageBucketFileDefinition() *FilestorageBucketFileSourceDefinition {
	return def.IDefinition.(*FilestorageBucketFileSourceDefinition)
}
