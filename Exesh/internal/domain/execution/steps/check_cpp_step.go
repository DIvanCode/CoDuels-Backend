package steps

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/sources"
	"fmt"
)

type CheckCppStep struct {
	execution.StepDetails
	CompiledChecker execution.Source `json:"compiled_checker"`
	CorrectOutput   execution.Source `json:"correct_output"`
	SuspectOutput   execution.Source `json:"suspect_output"`
}

func (step CheckCppStep) GetSources() []execution.Source {
	return []execution.Source{step.CompiledChecker, step.CorrectOutput, step.SuspectOutput}
}

func (step CheckCppStep) GetDependencies() []execution.StepName {
	return getDependencies(step)
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

	if step.CompiledChecker, err = sources.UnmarshalSourceJSON(attributes.CompiledChecker); err != nil {
		return fmt.Errorf("failed to unmarshal compiled_checker source: %w", err)
	}
	if step.CorrectOutput, err = sources.UnmarshalSourceJSON(attributes.CorrectOutput); err != nil {
		return fmt.Errorf("failed to unmarshal correct_output source: %w", err)
	}
	if step.SuspectOutput, err = sources.UnmarshalSourceJSON(attributes.SuspectOutput); err != nil {
		return fmt.Errorf("failed to unmarshal suspect_output source: %w", err)
	}

	return nil
}
