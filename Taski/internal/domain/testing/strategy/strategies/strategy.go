package strategies

import (
	"encoding/json"
	"fmt"
	"taski/internal/domain/task"
	"taski/internal/domain/testing/strategy"
)

type TestingStrategy struct {
	strategy.ITestingStrategy
}

func (ts TestingStrategy) MarshalJSON() ([]byte, error) {
	if ts.ITestingStrategy == nil {
		return []byte("null"), nil
	}

	return json.Marshal(ts.ITestingStrategy)
}

func (ts *TestingStrategy) UnmarshalJSON(data []byte) error {
	var details strategy.Details
	if err := json.Unmarshal(data, &details); err != nil {
		return fmt.Errorf("failed to unmarshal testing strategy details: %w", err)
	}

	switch details.TaskType {
	case task.WriteCode:
		ts.ITestingStrategy = &WriteCodeTaskTestingStrategy{}
	case task.PredictOutput:
		ts.ITestingStrategy = &PredictOutputTaskTestingStrategy{}
	case task.FindTest:
		ts.ITestingStrategy = &FindTestTaskTestingStrategy{}
	default:
		return fmt.Errorf("unknown task type: %s", details.TaskType)
	}

	if err := json.Unmarshal(data, ts.ITestingStrategy); err != nil {
		return fmt.Errorf("failed to unmarshal %s testing strategy: %w", details.TaskType, err)
	}

	return nil
}
