package execution

import (
	"time"
)

type (
	Execution struct {
		ID          ID
		Steps       []Step
		Status      Status
		CreatedAt   time.Time
		ScheduledAt *time.Time
		FinishedAt  *time.Time
	}

	Status string
)

const (
	StatusNewExecution       Status = "new"
	StatusScheduledExecution Status = "scheduled"
	StatusFinishedExecution  Status = "finished"
)

func NewExecution(steps []Step) Execution {
	return Execution{
		ID:          newID(),
		Steps:       steps,
		Status:      StatusNewExecution,
		CreatedAt:   time.Now(),
		ScheduledAt: nil,
		FinishedAt:  nil,
	}
}
