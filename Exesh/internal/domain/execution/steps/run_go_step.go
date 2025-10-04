package steps

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/sources"
	"fmt"
)

type RunGoStep struct {
	execution.StepDetails
	Code        execution.Source `json:"code"`
	RunInput    execution.Source `json:"run_input"`
	TimeLimit   int              `json:"time_limit"`
	MemoryLimit int              `json:"memory_limit"`
	ShowOutput  bool             `json:"show_output"`
}

func (step RunGoStep) GetSources() []execution.Source {
	return []execution.Source{step.Code, step.RunInput}
}

func (step RunGoStep) GetDependencies() []execution.StepName {
	return getDependencies(step)
}

func (step RunGoStep) GetAttributes() map[string]any {
	return map[string]any{
		"time_limit":   step.TimeLimit,
		"memory_limit": step.MemoryLimit,
		"show_output":  step.ShowOutput,
	}
}

func (step *RunGoStep) UnmarshalJSON(data []byte) error {
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

	if step.Code, err = sources.UnmarshalSourceJSON(attributes.Code); err != nil {
		return fmt.Errorf("failed to unmarshal code source: %w", err)
	}
	if step.RunInput, err = sources.UnmarshalSourceJSON(attributes.RunInput); err != nil {
		return fmt.Errorf("failed to unmarshal run_input source: %w", err)
	}
	step.TimeLimit = attributes.TimeLimit
	step.MemoryLimit = attributes.MemoryLimit
	step.ShowOutput = attributes.ShowOutput

	return nil
}
