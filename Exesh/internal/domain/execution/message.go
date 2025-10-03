package execution

type (
	Message interface {
		GetType() MessageType
		GetExecutionID() ID
	}

	MessageDetails struct {
		ExecutionID ID          `json:"execution_id"`
		Type        MessageType `json:"type"`
	}

	MessageType string

	MessageStatus string
)

const (
	StartExecutionMessage  MessageType = "start"
	CompileStepMessage     MessageType = "compile"
	RunStepMessage         MessageType = "run"
	CheckStepMessage       MessageType = "check"
	FinishExecutionMessage MessageType = "finish"
)

func (m MessageDetails) GetType() MessageType {
	return m.Type
}

func (m MessageDetails) GetExecutionID() ID {
	return m.ExecutionID
}
