package steps

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/sources"
	"fmt"
)

type RunGoStep struct {
	execution.StepDetails
	CodeSource  execution.Source `json:"code_source"`
	InputSource execution.Source `json:"input_source"`
	TimeLimit   int              `json:"time_limit"`
	MemoryLimit int              `json:"memory_limit"`
	ShowOutput  bool             `json:"show_output"`
}

func (s *RunGoStep) UnmarshalJSON(data []byte) error {
	var err error
	if err = json.Unmarshal(data, &s.StepDetails); err != nil {
		return fmt.Errorf("failed to unmarshal step details: %w", err)
	}

	attributes := struct {
		CodeSource  json.RawMessage `json:"code_source"`
		InputSource json.RawMessage `json:"input_source"`
		TimeLimit   int             `json:"time_limit"`
		MemoryLimit int             `json:"memory_limit"`
		ShowOutput  bool            `json:"show_output"`
	}{}
	if err = json.Unmarshal(data, &attributes); err != nil {
		return fmt.Errorf("failed to unmarshal %s step attributes: %w", s.Type, err)
	}

	if s.CodeSource, err = sources.UnmarshalSourceJSON(attributes.CodeSource); err != nil {
		return fmt.Errorf("failed to unmarshal code source: %w", err)
	}
	if s.InputSource, err = sources.UnmarshalSourceJSON(attributes.InputSource); err != nil {
		return fmt.Errorf("failed to unmarshal input source: %w", err)
	}
	s.TimeLimit = attributes.TimeLimit
	s.MemoryLimit = attributes.MemoryLimit
	s.ShowOutput = attributes.ShowOutput

	return nil
}
