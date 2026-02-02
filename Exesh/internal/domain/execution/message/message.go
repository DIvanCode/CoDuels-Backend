package message

import "exesh/internal/domain/execution"

type (
	IMessage interface {
		GetType() Type
		GetExecutionID() execution.ID
	}

	Details struct {
		ExecutionID execution.ID `json:"execution_id"`
		Type        Type         `json:"type"`
	}

	Type   string
	Status string
)

const (
	StartExecution  Type = "start"
	CompileJob      Type = "compile"
	RunJob          Type = "run"
	CheckJob        Type = "check"
	FinishExecution Type = "finish"
)

func (msg *Details) GetType() Type {
	return msg.Type
}

func (msg *Details) GetExecutionID() execution.ID {
	return msg.ExecutionID
}
