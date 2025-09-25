package steps

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/sources"
	"fmt"
)

type CheckCppStep struct {
	execution.StepDetails
	CheckerExeSource    execution.Source `json:"checker_exe_source"`
	CorrectOutputSource execution.Source `json:"correct_output_source"`
	SuspectOutputSource execution.Source `json:"suspect_output_source"`
}

func (s *CheckCppStep) UnmarshalJSON(data []byte) error {
	var err error
	if err = json.Unmarshal(data, &s.StepDetails); err != nil {
		return fmt.Errorf("failed to unmarshal step details: %w", err)
	}

	attributes := struct {
		CheckerExeSource    json.RawMessage `json:"checker_exe_source"`
		CorrectOutputSource json.RawMessage `json:"correct_output_source"`
		SuspectOutputSource json.RawMessage `json:"suspect_output_source"`
	}{}
	if err = json.Unmarshal(data, &attributes); err != nil {
		return fmt.Errorf("failed to unmarshal %s step attributes: %w", s.Type, err)
	}

	if s.CheckerExeSource, err = sources.UnmarshalSourceJSON(attributes.CheckerExeSource); err != nil {
		return fmt.Errorf("failed to unmarshal checker exe source: %w", err)
	}
	if s.CorrectOutputSource, err = sources.UnmarshalSourceJSON(attributes.CorrectOutputSource); err != nil {
		return fmt.Errorf("failed to unmarshal correct output source: %w", err)
	}
	if s.SuspectOutputSource, err = sources.UnmarshalSourceJSON(attributes.SuspectOutputSource); err != nil {
		return fmt.Errorf("failed to unmarshal suspect output source: %w", err)
	}

	return nil
}
