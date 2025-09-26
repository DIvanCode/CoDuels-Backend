package steps

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/inputs"
	"fmt"
)

type RunPyStep struct {
	execution.StepDetails
	Code        execution.Input `json:"code"`
	RunInput    execution.Input `json:"run_input"`
	TimeLimit   int             `json:"time_limit"`
	MemoryLimit int             `json:"memory_limit"`
	ShowOutput  bool            `json:"show_output"`
}

func (step RunPyStep) GetAttributes() map[string]any {
	return map[string]any{
		"time_limit":   step.TimeLimit,
		"memory_limit": step.MemoryLimit,
		"show_output":  step.ShowOutput,
	}
}

func (step *RunPyStep) UnmarshalJSON(data []byte) error {
	var err error
	if err = json.Unmarshal(data, &step.StepDetails); err != nil {
		return fmt.Errorf("failed to unmarshal step details: %w", err)
	}

	attributes := struct {
		Code        json.RawMessage `json:"code"`
		RunInput    json.RawMessage `json:"run_input"`
		TimeLimit   int             `json:"time_limit"`
		MemoryLimit int             `json:"memory_limit"`
		ShowOutput  bool            `json:"show_output"`
	}{}
	if err = json.Unmarshal(data, &attributes); err != nil {
		return fmt.Errorf("failed to unmarshal %s step attributes: %w", step.Type, err)
	}

	if step.Code, err = inputs.UnmarshalInputJSON(attributes.Code); err != nil {
		return fmt.Errorf("failed to unmarshal code input: %w", err)
	}
	if step.RunInput, err = inputs.UnmarshalInputJSON(attributes.RunInput); err != nil {
		return fmt.Errorf("failed to unmarshal run_input input: %w", err)
	}
	step.TimeLimit = attributes.TimeLimit
	step.MemoryLimit = attributes.MemoryLimit
	step.ShowOutput = attributes.ShowOutput

	return nil
}
