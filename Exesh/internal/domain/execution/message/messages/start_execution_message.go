package messages

import (
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/message"
)

type StartExecutionMessage struct {
	message.Details
}

func NewStartExecutionMessage(executionID execution.ID) Message {
	return Message{
		&StartExecutionMessage{
			Details: message.Details{
				ExecutionID: executionID,
				Type:        message.StartExecution,
			},
		},
	}
}
