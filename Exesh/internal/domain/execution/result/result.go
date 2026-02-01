package result

import (
	"errors"
	"exesh/internal/domain/execution/job"
	"time"
)

type (
	IResult interface {
		GetType() Type
		GetJobID() job.ID
		GetStatus() job.Status
		GetDoneAt() time.Time
		GetError() error
	}

	Details struct {
		Type   Type       `json:"type"`
		ID     job.ID     `json:"id"`
		Status job.Status `json:"status"`
		DoneAt time.Time  `json:"done_at"`
		Error  string     `json:"error,omitempty"`
	}

	Type string
)

const (
	Compile Type = "compile"
	Run     Type = "run"
	Check   Type = "check"
)

func (res *Details) GetType() Type {
	return res.Type
}

func (res *Details) GetJobID() job.ID {
	return res.ID
}

func (res *Details) GetStatus() job.Status {
	return res.Status
}

func (res *Details) GetDoneAt() time.Time {
	return res.DoneAt
}

func (res *Details) GetError() error {
	if res.Error == "" {
		return nil
	}
	return errors.New(res.Error)
}
