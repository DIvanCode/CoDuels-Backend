package messages

import (
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/message"
)

type CheckJobMessage struct {
	message.Details
	JobName     job.DefinitionName `json:"job"`
	CheckStatus job.Status         `json:"status"`
}

func NewCheckJobMessage(
	executionID execution.ID,
	jobName job.DefinitionName,
	status job.Status,
) Message {
	return Message{
		&CheckJobMessage{
			Details: message.Details{
				ExecutionID: executionID,
				Type:        message.CheckJob,
			},
			JobName:     jobName,
			CheckStatus: status,
		},
	}
}
