package messages

import (
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/results"
)

type CompileStepMessage struct {
	execution.MessageDetails
	StepName      execution.StepName    `json:"step_name"`
	CompileStatus results.CompileStatus `json:"status"`
	Error         string                `json:"error,omitempty"`
}

func NewCompileStepMessage(
	executionID execution.ID,
	stepName execution.StepName,
	status results.CompileStatus,
) CompileStepMessage {
	return CompileStepMessage{
		MessageDetails: execution.MessageDetails{
			ExecutionID: executionID,
			Type:        execution.CompileStepMessage,
		},
		StepName:      stepName,
		CompileStatus: status,
	}
}

func NewCompileStepMessageError(
	executionID execution.ID,
	stepName execution.StepName,
	status results.CompileStatus,
	error string,
) CompileStepMessage {
	return CompileStepMessage{
		MessageDetails: execution.MessageDetails{
			ExecutionID: executionID,
			Type:        execution.CompileStepMessage,
		},
		StepName:      stepName,
		CompileStatus: status,
		Error:         error,
	}
}
