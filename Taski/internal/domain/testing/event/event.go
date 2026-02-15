package event

import (
	"taski/internal/domain/testing/execution"
)

type (
	IEvent interface {
		GetType() Type
		GetExecutionID() execution.ID
	}

	Details struct {
		Type        Type         `json:"type"`
		ExecutionID execution.ID `json:"execution_id"`
	}

	Type string
)

const (
	StartExecution  Type = "start"
	CompileJob      Type = "compile"
	RunJob          Type = "run"
	CheckJob        Type = "check"
	FinishExecution Type = "finish"
)

func (evt *Details) GetType() Type {
	return evt.Type
}

func (evt *Details) GetExecutionID() execution.ID {
	return evt.ExecutionID
}
