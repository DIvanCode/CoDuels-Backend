package messages

import (
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/message"
)

type FinishExecutionMessage struct {
	message.Details
	Error string `json:"error,omitempty"`
}

func NewFinishExecutionMessageOk(executionID execution.ID) Message {
	return Message{
		&FinishExecutionMessage{
			Details: message.Details{
				ExecutionID: executionID,
				Type:        message.FinishExecution,
			},
		},
	}
}

func NewFinishExecutionMessageError(executionID execution.ID, error string) Message {
	return Message{
		&FinishExecutionMessage{
			Details: message.Details{
				ExecutionID: executionID,
				Type:        message.FinishExecution,
			},
			Error: error,
		},
	}
}
