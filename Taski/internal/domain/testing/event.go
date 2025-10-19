package testing

type (
	Event interface {
		GetType() EventType
		GetExecutionID() ExecutionID
	}

	EventDetails struct {
		ExecutionID ExecutionID `json:"execution_id"`
		Type        EventType   `json:"type"`
	}

	EventType string

	EventStatus string
)

const (
	StartExecutionEvent  EventType = "start"
	CompileStepEvent     EventType = "compile"
	RunStepEvent         EventType = "run"
	CheckStepEvent       EventType = "check"
	FinishExecutionEvent EventType = "finish"
)

func (e EventDetails) GetType() EventType {
	return e.Type
}

func (e EventDetails) GetExecutionID() ExecutionID {
	return e.ExecutionID
}
