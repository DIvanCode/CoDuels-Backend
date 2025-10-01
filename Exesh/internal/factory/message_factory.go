package factory

import (
	"exesh/internal/domain/execution"
	"fmt"
	"log/slog"
)

type MessageFactory struct {
	log *slog.Logger
}

func NewMessageFactory(log *slog.Logger) *MessageFactory {
	return &MessageFactory{
		log: log,
	}
}

func (f *MessageFactory) CreateExecutionStarted(execCtx execution.Context) (execution.Message, error) {
	return nil, fmt.Errorf("not implemented")
}

func (f *MessageFactory) CreateForStep(execCtx execution.Context, step execution.Step, result execution.Result) (execution.Message, error) {
	return nil, fmt.Errorf("not implemented")
}

func (f *MessageFactory) CreateExecutionFinished(execCtx execution.Context) (execution.Message, error) {
	return nil, fmt.Errorf("not implemented")
}
