package factory

import (
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/messages"
	"exesh/internal/domain/execution/results"
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

func (f *MessageFactory) CreateExecutionStarted(execCtx *execution.Context) execution.Message {
	return messages.NewStartExecutionMessage(execCtx.ExecutionID)
}

func (f *MessageFactory) CreateForStep(execCtx *execution.Context, step execution.Step, result execution.Result) (execution.Message, error) {
	switch result.GetType() {
	case execution.CompileResult:
		typedResult := result.(*results.CompileResult)
		if typedResult.Status == results.CompileStatusOK {
			return messages.NewCompileStepMessage(execCtx.ExecutionID, step.GetName()), nil
		} else if typedResult.Status == results.CompileStatusCE {
			return messages.NewCompileStepMessageError(execCtx.ExecutionID, step.GetName(), typedResult.CompilationError), nil
		} else {
			return nil, fmt.Errorf("unknown compile status: %s", typedResult.Status)
		}
	case execution.RunResult:
		typedResult := result.(*results.RunResult)
		if typedResult.Status == results.RunStatusOK && typedResult.HasOutput {
			return messages.NewRunStepMessageWithOutput(execCtx.ExecutionID, step.GetName(), typedResult.Output), nil
		} else {
			return messages.NewRunStepMessage(execCtx.ExecutionID, step.GetName(), typedResult.Status), nil
		}
	case execution.CheckResult:
		typedResult := result.(*results.CheckResult)
		return messages.NewCheckStepMessage(execCtx.ExecutionID, step.GetName(), typedResult.Status), nil
	default:
		return nil, fmt.Errorf("unknown result type %s", result.GetType())
	}
}

func (f *MessageFactory) CreateExecutionFinished(execCtx *execution.Context) execution.Message {
	return messages.NewFinishExecutionMessage(execCtx.ExecutionID)
}

func (f *MessageFactory) CreateExecutionFinishedError(execCtx *execution.Context, err string) execution.Message {
	return messages.NewFinishExecutionMessageError(execCtx.ExecutionID, err)
}
