package messages

import "exesh/internal/domain/execution"

type StartExecutionMessage struct {
	execution.MessageDetails
}

func NewStartExecutionMessage(executionID execution.ID) StartExecutionMessage {
	return StartExecutionMessage{
		MessageDetails: execution.MessageDetails{
			ExecutionID: executionID,
			Type:        execution.StartExecutionMessage,
		},
	}
}
