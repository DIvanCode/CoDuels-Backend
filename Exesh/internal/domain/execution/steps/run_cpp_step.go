package steps

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/sources"
	"fmt"
)

type RunCppStep struct {
	execution.StepDetails
	ExeSource   execution.Source `json:"exe_source"`
	InputSource execution.Source `json:"input_source"`
	TimeLimit   int              `json:"time_limit"`
	MemoryLimit int              `json:"memory_limit"`
	ShowOutput  bool             `json:"show_output"`
}

func (s *RunCppStep) UnmarshalJSON(data []byte) error {
	var err error
	if err = json.Unmarshal(data, &s.StepDetails); err != nil {
		return fmt.Errorf("failed to unmarshal step details: %w", err)
	}

	attributes := struct {
		ExeSource   json.RawMessage `json:"exe_source"`
		InputSource json.RawMessage `json:"input_source"`
		TimeLimit   int             `json:"time_limit"`
		MemoryLimit int             `json:"memory_limit"`
		ShowOutput  bool            `json:"show_output"`
	}{}
	if err = json.Unmarshal(data, &attributes); err != nil {
		return fmt.Errorf("failed to unmarshal %s step attributes: %w", s.Type, err)
	}

	if s.ExeSource, err = sources.UnmarshalSourceJSON(attributes.ExeSource); err != nil {
		return fmt.Errorf("failed to unmarshal exe source: %w", err)
	}
	if s.InputSource, err = sources.UnmarshalSourceJSON(attributes.InputSource); err != nil {
		return fmt.Errorf("failed to unmarshal input source: %w", err)
	}
	s.TimeLimit = attributes.TimeLimit
	s.MemoryLimit = attributes.MemoryLimit
	s.ShowOutput = attributes.ShowOutput

	return nil
}
