package messages

import (
	"taski/internal/domain/testing"
	"taski/internal/domain/testing/message"
)

type FinishTestingMessage struct {
	message.Details
	Verdict string `json:"verdict"`
	Error   string `json:"error,omitempty"`
	Message string `json:"message,omitempty"`
}

func NewFinishTestingMessage(externalID testing.ExternalSolutionID, verdict string) Message {
	return Message{
		&FinishTestingMessage{
			Details: message.Details{
				ExternalID: externalID,
				Type:       message.FinishTestingMessage,
			},
			Verdict: verdict,
		},
	}
}

func NewFinishTestingMessageWithMessage(externalID testing.ExternalSolutionID, verdict string, msg string) Message {
	return Message{
		&FinishTestingMessage{
			Details: message.Details{
				ExternalID: externalID,
				Type:       message.FinishTestingMessage,
			},
			Verdict: verdict,
			Message: msg,
		},
	}
}

func NewFinishTestingMessageWithError(externalID testing.ExternalSolutionID, verdict string, err string) Message {
	return Message{
		&FinishTestingMessage{
			Details: message.Details{
				ExternalID: externalID,
				Type:       message.FinishTestingMessage,
			},
			Verdict: verdict,
			Error:   err,
		},
	}
}
