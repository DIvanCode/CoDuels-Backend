package messages

import (
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/results"
)

type RunStepMessage struct {
	execution.MessageDetails
	StepName  execution.StepName `json:"step_name"`
	RunStatus results.RunStatus  `json:"status"`
	Output    string             `json:"output,omitempty"`
}

func NewRunStepMessage(
	executionID execution.ID,
	stepName execution.StepName,
	status results.RunStatus,
) RunStepMessage {
	return RunStepMessage{
		MessageDetails: execution.MessageDetails{
			ExecutionID: executionID,
			Type:        execution.RunStepMessage,
		},
		StepName:  stepName,
		RunStatus: status,
	}
}

func NewRunStepMessageWithOutput(
	executionID execution.ID,
	stepName execution.StepName,
	output string,
) RunStepMessage {
	return RunStepMessage{
		MessageDetails: execution.MessageDetails{
			ExecutionID: executionID,
			Type:        execution.RunStepMessage,
		},
		StepName:  stepName,
		RunStatus: results.RunStatusOK,
		Output:    output,
	}
}
