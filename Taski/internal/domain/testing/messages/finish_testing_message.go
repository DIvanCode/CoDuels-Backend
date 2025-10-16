package messages

import "taski/internal/domain/testing"

type (
	FinishTestingMessage struct {
		testing.MessageDetails
		Verdict string `json:"verdict,omitempty"`
		Message string `json:"message,omitempty"`
		Error   string `json:"error,omitempty"`
	}
)

func NewFinishTestingMessage(solutionID testing.SolutionID, verdict string) FinishTestingMessage {
	return FinishTestingMessage{
		MessageDetails: testing.MessageDetails{
			SolutionID: solutionID,
			Type:       testing.FinishTestingMessage,
		},
		Verdict: verdict,
	}
}

func NewFinishTestingMessageWithMessage(solutionID testing.SolutionID, verdict, message string) FinishTestingMessage {
	return FinishTestingMessage{
		MessageDetails: testing.MessageDetails{
			SolutionID: solutionID,
			Type:       testing.FinishTestingMessage,
		},
		Verdict: verdict,
		Message: message,
	}
}

func NewFinishTestingMessageWithError(solutionID testing.SolutionID, err string) FinishTestingMessage {
	return FinishTestingMessage{
		MessageDetails: testing.MessageDetails{
			SolutionID: solutionID,
			Type:       testing.FinishTestingMessage,
		},
		Error: err,
	}
}
