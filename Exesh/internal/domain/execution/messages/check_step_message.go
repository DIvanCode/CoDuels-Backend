package messages

import (
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/results"
)

type CheckStepMessage struct {
	execution.MessageDetails
	StepName    execution.StepName  `json:"step_name"`
	CheckStatus results.CheckStatus `json:"status"`
}

func NewCheckStepMessage(
	executionID execution.ID,
	stepName execution.StepName,
	status results.CheckStatus,
) CheckStepMessage {
	return CheckStepMessage{
		MessageDetails: execution.MessageDetails{
			ExecutionID: executionID,
			Type:        execution.CheckStepMessage,
		},
		StepName:    stepName,
		CheckStatus: status,
	}
}
