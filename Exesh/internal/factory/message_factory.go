package factory

import (
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/message/messages"
	"exesh/internal/domain/execution/result"
	"exesh/internal/domain/execution/result/results"
	"fmt"
)

type MessageFactory struct{}

func NewMessageFactory() *MessageFactory {
	return &MessageFactory{}
}

func (f *MessageFactory) CreateExecutionStarted(executionID execution.ID) messages.Message {
	return messages.NewStartExecutionMessage(executionID)
}

func (f *MessageFactory) CreateForJob(
	executionID execution.ID,
	jobName job.DefinitionName,
	res results.Result,
) (messages.Message, error) {
	var msg messages.Message

	switch res.GetType() {
	case result.Compile:
		typedRes := res.AsCompile()
		switch typedRes.Status {
		case job.StatusOK:
			msg = messages.NewCompileJobMessageOk(executionID, jobName)
		case job.StatusCE:
			msg = messages.NewCompileJobMessageError(executionID, jobName, typedRes.CompilationError)
		default:
			return msg, fmt.Errorf("unknown compile status: %s", typedRes.Status)
		}
	case result.Run:
		typedRes := res.AsRun()
		if !typedRes.HasOutput {
			msg = messages.NewRunJobMessage(executionID, jobName, typedRes.Status)
		} else {
			msg = messages.NewRunJobMessageWithOutput(executionID, jobName, typedRes.Output)
		}
	case result.Check:
		typedRes := res.AsCheck()
		msg = messages.NewCheckJobMessage(executionID, jobName, typedRes.Status)
	default:
		return msg, fmt.Errorf("unknown result type %s", res.GetType())
	}

	return msg, nil
}

func (f *MessageFactory) CreateExecutionFinished(executionID execution.ID) messages.Message {
	return messages.NewFinishExecutionMessageOk(executionID)
}

func (f *MessageFactory) CreateExecutionFinishedError(executionID execution.ID, err string) messages.Message {
	return messages.NewFinishExecutionMessageError(executionID, err)
}
