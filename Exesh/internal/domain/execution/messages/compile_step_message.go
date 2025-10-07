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
) CompileStepMessage {
	return CompileStepMessage{
		MessageDetails: execution.MessageDetails{
			ExecutionID: executionID,
			Type:        execution.CompileStepMessage,
		},
		StepName:      stepName,
		CompileStatus: results.CompileStatusOK,
	}
}

func NewCompileStepMessageError(
	executionID execution.ID,
	stepName execution.StepName,
	err string,
) CompileStepMessage {
	return CompileStepMessage{
		MessageDetails: execution.MessageDetails{
			ExecutionID: executionID,
			Type:        execution.CompileStepMessage,
		},
		StepName:      stepName,
		CompileStatus: results.CompileStatusCE,
		Error:         err,
	}
}
