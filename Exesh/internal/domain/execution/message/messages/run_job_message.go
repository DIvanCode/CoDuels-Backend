package messages

import (
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/message"
)

type RunJobMessage struct {
	message.Details
	JobName   job.DefinitionName `json:"job"`
	RunStatus job.Status         `json:"status"`
	Output    string             `json:"output,omitempty"`
}

func NewRunJobMessage(
	executionID execution.ID,
	jobName job.DefinitionName,
	status job.Status,
) Message {
	return Message{
		&RunJobMessage{
			Details: message.Details{
				ExecutionID: executionID,
				Type:        message.RunJob,
			},
			JobName:   jobName,
			RunStatus: status,
		},
	}
}

func NewRunJobMessageWithOutput(
	executionID execution.ID,
	jobName job.DefinitionName,
	output string,
) Message {
	return Message{
		&RunJobMessage{
			Details: message.Details{
				ExecutionID: executionID,
				Type:        message.RunJob,
			},
			JobName:   jobName,
			RunStatus: job.StatusOK,
			Output:    output,
		},
	}
}
