package messages

import (
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/message"
)

type CompileJobMessage struct {
	message.Details
	JobName          job.DefinitionName `json:"job"`
	CompileStatus    job.Status         `json:"status"`
	CompilationError *string            `json:"compilation_error,omitempty"`
}

func NewCompileJobMessageOk(
	executionID execution.ID,
	jobName job.DefinitionName,
) Message {
	return Message{
		&CompileJobMessage{
			Details: message.Details{
				ExecutionID: executionID,
				Type:        message.CompileJob,
			},
			JobName:       jobName,
			CompileStatus: job.StatusOK,
		},
	}
}

func NewCompileJobMessageError(
	executionID execution.ID,
	jobName job.DefinitionName,
	compilationError string,
) Message {
	return Message{
		&CompileJobMessage{
			Details: message.Details{
				ExecutionID: executionID,
				Type:        message.CompileJob,
			},
			JobName:          jobName,
			CompileStatus:    job.StatusCE,
			CompilationError: &compilationError,
		},
	}
}
