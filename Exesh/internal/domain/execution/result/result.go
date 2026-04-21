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
		GetElapsedTime() int
		GetUsedMemory() int
		GetError() error
	}

	Details struct {
		Type        Type       `json:"type"`
		JobID       job.ID     `json:"job_id"`
		Status      job.Status `json:"status"`
		DoneAt      time.Time  `json:"done_at"`
		ElapsedTime int        `json:"elapsed_time"`
		UsedMemory  int        `json:"used_memory"`
		Error       string     `json:"error,omitempty"`
	}

	Type string
)

const (
	Compile Type = "compile"
	Run     Type = "run"
	Check   Type = "check"
	Chain   Type = "chain"
	Unknown Type = "unknown"
)

func (res *Details) GetType() Type {
	return res.Type
}

func (res *Details) GetJobID() job.ID {
	return res.JobID
}

func (res *Details) GetStatus() job.Status {
	return res.Status
}

func (res *Details) GetDoneAt() time.Time {
	return res.DoneAt
}

func (res *Details) GetElapsedTime() int {
	return res.ElapsedTime
}

func (res *Details) GetUsedMemory() int {
	return res.UsedMemory
}

func (res *Details) GetError() error {
	if res.Error == "" {
		return nil
	}
	return errors.New(res.Error)
}
