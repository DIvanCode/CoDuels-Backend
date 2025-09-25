package steps

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"fmt"
)

func UnmarshalStepJSON(data []byte) (step execution.Step, err error) {
	var details execution.StepDetails
	if err = json.Unmarshal(data, &details); err != nil {
		err = fmt.Errorf("failed to unmarshal step details: %w", err)
		return
	}

	switch details.Type {
	case execution.CompileCppStepType:
		step = &CompileCppStep{}
	case execution.RunCppStepType:
		step = &RunCppStep{}
	case execution.RunGoStepType:
		step = &RunGoStep{}
	case execution.RunPyStepType:
		step = &RunPyStep{}
	case execution.CheckCppStepType:
		step = &CheckCppStep{}
	default:
		err = fmt.Errorf("unknown step type: %s", details.Type)
		return
	}

	if err = json.Unmarshal(data, step); err != nil {
		err = fmt.Errorf("failed to unmarshal %s step: %w", details.Type, err)
		return
	}
	return
}

func UnmarshalStepsJSON(data []byte) (stepsArray []execution.Step, err error) {
	var array []json.RawMessage
	if err = json.Unmarshal(data, &array); err != nil {
		err = fmt.Errorf("failed to unmarshal array: %w", err)
		return
	}

	stepsArray = make([]execution.Step, 0, len(array))
	for _, item := range array {
		var step execution.Step
		step, err = UnmarshalStepJSON(item)
		if err != nil {
			err = fmt.Errorf("failed to unmarshal step: %w", err)
			return
		}
		stepsArray = append(stepsArray, step)
	}
	return
}
