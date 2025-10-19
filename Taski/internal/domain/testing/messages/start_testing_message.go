package messages

import "taski/internal/domain/testing"

type StartTestingMessage struct {
	testing.MessageDetails
}

func NewStartTestingMessage(solutionID testing.SolutionID) StartTestingMessage {
	return StartTestingMessage{
		MessageDetails: testing.MessageDetails{
			SolutionID: solutionID,
			Type:       testing.StartTestingMessage,
		},
	}
}
