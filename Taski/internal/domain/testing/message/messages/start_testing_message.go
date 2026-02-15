package messages

import (
	"taski/internal/domain/testing"
	"taski/internal/domain/testing/message"
)

type StartTestingMessage struct {
	message.Details
}

func NewStartTestingMessage(externalID testing.ExternalSolutionID) Message {
	return Message{
		&StartTestingMessage{
			Details: message.Details{
				ExternalID: externalID,
				Type:       message.StartTestingMessage,
			},
		},
	}
}
