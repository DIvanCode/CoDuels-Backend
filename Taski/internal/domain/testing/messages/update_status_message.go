package messages

import "taski/internal/domain/testing"

type UpdateStatusMessage struct {
	testing.MessageDetails
	Message string `json:"message"`
}

func NewUpdateStatusMessage(solutionID testing.SolutionID, message string) UpdateStatusMessage {
	return UpdateStatusMessage{
		MessageDetails: testing.MessageDetails{
			SolutionID: solutionID,
			Type:       testing.UpdateStatusMessage,
		},
		Message: message,
	}
}
