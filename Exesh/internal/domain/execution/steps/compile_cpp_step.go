package steps

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/sources"
	"fmt"
)

type CompileCppStep struct {
	execution.StepDetails
	Code execution.Source `json:"code"`
}

func (step CompileCppStep) GetSources() []execution.Source {
	return []execution.Source{step.Code}
}

func (step CompileCppStep) GetDependencies() []execution.StepName {
	return getDependencies(step)
}

func (step CompileCppStep) GetAttributes() map[string]any {
	return map[string]any{}
}

func (step *CompileCppStep) UnmarshalJSON(data []byte) error {
	var err error
	if err = json.Unmarshal(data, &step.StepDetails); err != nil {
		return fmt.Errorf("failed to unmarshal step details: %w", err)
	}

	attributes := struct {
		Code json.RawMessage `json:"code"`
	}{}
	if err = json.Unmarshal(data, &attributes); err != nil {
		return fmt.Errorf("failed to unmarshal %s step attributes: %w", step.Type, err)
	}

	if step.Code, err = sources.UnmarshalSourceJSON(attributes.Code); err != nil {
		return fmt.Errorf("failed to unmarshal code source: %w", err)
	}

	return nil
}
