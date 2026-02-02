package execution

import (
	"database/sql/driver"
	"encoding/json"
	"exesh/internal/domain/execution/job/jobs"
	"fmt"
)

type (
	StageDefinition struct {
		Name StageName         `json:"name"`
		Deps []StageName       `json:"deps"`
		Jobs []jobs.Definition `json:"jobs"`
	}

	StageDefinitions []StageDefinition
)

func (s StageDefinitions) Value() (driver.Value, error) {
	b, err := json.Marshal(s)
	if err != nil {
		return nil, fmt.Errorf("failed to marshal stage definitions: %w", err)
	}
	return b, nil
}

func (s *StageDefinitions) Scan(src any) error {
	if src == nil {
		*s = nil
		return nil
	}

	var data []byte

	switch v := src.(type) {
	case []byte:
		data = v
	case string:
		data = []byte(v)
	default:
		return fmt.Errorf("failed to scan stage definitions from type %T", src)
	}

	if len(data) == 0 {
		*s = nil
		return nil
	}

	var out StageDefinitions
	if err := json.Unmarshal(data, &out); err != nil {
		return fmt.Errorf("failed to unmarshal stage definitions: %w", err)
	}

	*s = out
	return nil
}
