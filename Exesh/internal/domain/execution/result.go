package execution

import (
	"errors"
	"time"
)

type (
	Result interface {
		GetJobID() JobID
		GetType() ResultType
		GetDoneAt() time.Time
		GetError() error
		ShouldFinishExecution() bool
	}

	ResultDetails struct {
		ID     JobID      `json:"id"`
		Type   ResultType `json:"type"`
		DoneAt time.Time  `json:"done_at"`
		Error  string     `json:"error,omitempty"`
	}

	ResultType string
)

const (
	CompileResult ResultType = "compile"
	RunResult     ResultType = "run"
	CheckResult   ResultType = "check"
)

func (r ResultDetails) GetJobID() JobID {
	return r.ID
}

func (r ResultDetails) GetType() ResultType {
	return r.Type
}

func (r ResultDetails) GetDoneAt() time.Time {
	return r.DoneAt
}

func (r ResultDetails) GetError() error {
	if r.Error == "" {
		return nil
	}
	return errors.New(r.Error)
}

func (r ResultDetails) ShouldFinishExecution() bool {
	panic("this panic would never happen!")
}
