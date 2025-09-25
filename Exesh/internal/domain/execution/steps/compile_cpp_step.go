package steps

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/sources"
	"fmt"
)

type CompileCppStep struct {
	execution.StepDetails
	CodeSource execution.Source `json:"code_source"`
}

func (s *CompileCppStep) UnmarshalJSON(data []byte) error {
	var err error
	if err = json.Unmarshal(data, &s.StepDetails); err != nil {
		return fmt.Errorf("failed to unmarshal step details: %w", err)
	}

	attributes := struct {
		CodeSource json.RawMessage `json:"code_source"`
	}{}
	if err = json.Unmarshal(data, &attributes); err != nil {
		return fmt.Errorf("failed to unmarshal %s step attributes: %w", s.Type, err)
	}

	if s.CodeSource, err = sources.UnmarshalSourceJSON(attributes.CodeSource); err != nil {
		return fmt.Errorf("failed to unmarshal code source: %w", err)
	}

	return nil
}
