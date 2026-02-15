package message

import "taski/internal/domain/testing"

type (
	IMessage interface {
		GetType() Type
		GetExternalID() testing.ExternalSolutionID
	}

	Details struct {
		ExternalID testing.ExternalSolutionID `json:"solution_id"`
		Type       Type                       `json:"type"`
	}

	Type string
)

const (
	StartTestingMessage  Type = "start"
	UpdateStatusMessage  Type = "status"
	FinishTestingMessage Type = "finish"
)

func (m *Details) GetType() Type {
	return m.Type
}

func (m *Details) GetExternalID() testing.ExternalSolutionID {
	return m.ExternalID
}
