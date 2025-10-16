package testing

type (
	Message interface {
		GetType() MessageType
		GetSolutionID() SolutionID
	}

	MessageDetails struct {
		SolutionID SolutionID  `json:"solution_id"`
		Type       MessageType `json:"type"`
	}

	MessageType string

	MessageStatus string
)

const (
	StartTestingMessage  MessageType = "start"
	UpdateStatusMessage  MessageType = "status"
	FinishTestingMessage MessageType = "finish"
)

func (m MessageDetails) GetType() MessageType {
	return m.Type
}

func (m MessageDetails) GetSolutionID() SolutionID {
	return m.SolutionID
}
