package messages

import "exesh/internal/domain/execution"

type FinishExecutionMessage struct {
	execution.MessageDetails
	Error string `json:"error,omitempty"`
}

func NewFinishExecutionMessage(executionID execution.ID) FinishExecutionMessage {
	return FinishExecutionMessage{
		MessageDetails: execution.MessageDetails{
			ExecutionID: executionID,
			Type:        execution.FinishExecutionMessage,
		},
	}
}

func NewFinishExecutionMessageError(executionID execution.ID, error string) FinishExecutionMessage {
	return FinishExecutionMessage{
		MessageDetails: execution.MessageDetails{
			ExecutionID: executionID,
			Type:        execution.FinishExecutionMessage,
		},
		Error: error,
	}
}
