package messages

import (
	"taski/internal/domain/testing"
	"taski/internal/domain/testing/message"
)

type UpdateStatusMessage struct {
	message.Details
	Status string `json:"status"`
}

func NewUpdateStatusMessage(externalID testing.ExternalSolutionID, status string) Message {
	return Message{
		&UpdateStatusMessage{
			Details: message.Details{
				ExternalID: externalID,
				Type:       message.UpdateStatusMessage,
			},
			Status: status,
		},
	}
}
