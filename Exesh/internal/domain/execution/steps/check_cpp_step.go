package steps

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/inputs"
	"fmt"
)

type CheckCppStep struct {
	execution.StepDetails
	CompiledChecker execution.Input `json:"compiled_checker"`
	CorrectOutput   execution.Input `json:"correct_output"`
	SuspectOutput   execution.Input `json:"suspect_output"`
}

func (step CheckCppStep) GetAttributes() map[string]any {
	return map[string]any{}
}

func (step *CheckCppStep) UnmarshalJSON(data []byte) error {
	var err error
	if err = json.Unmarshal(data, &step.StepDetails); err != nil {
		return fmt.Errorf("failed to unmarshal step details: %w", err)
	}

	attributes := struct {
		CompiledChecker json.RawMessage `json:"compiled_checker"`
		CorrectOutput   json.RawMessage `json:"correct_output"`
		SuspectOutput   json.RawMessage `json:"suspect_output"`
	}{}
	if err = json.Unmarshal(data, &attributes); err != nil {
		return fmt.Errorf("failed to unmarshal %s step attributes: %w", step.Type, err)
	}

	if step.CompiledChecker, err = inputs.UnmarshalInputJSON(attributes.CompiledChecker); err != nil {
		return fmt.Errorf("failed to unmarshal compiled_checker input: %w", err)
	}
	if step.CorrectOutput, err = inputs.UnmarshalInputJSON(attributes.CorrectOutput); err != nil {
		return fmt.Errorf("failed to unmarshal correct_output input: %w", err)
	}
	if step.SuspectOutput, err = inputs.UnmarshalInputJSON(attributes.SuspectOutput); err != nil {
		return fmt.Errorf("failed to unmarshal suspect_output input: %w", err)
	}

	return nil
}
