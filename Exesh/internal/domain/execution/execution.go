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

func (e *Execution) SetScheduled(scheduledAt time.Time) {
	if e.Status == StatusFinishedExecution {
		return
	}

	e.Status = StatusScheduledExecution
	e.ScheduledAt = &scheduledAt
}

func (e *Execution) SetFinished(finishedAt time.Time) {
	if e.Status == StatusFinishedExecution {
		return
	}

	e.Status = StatusFinishedExecution
	e.ScheduledAt = &finishedAt
}

func (e *Execution) BuildContext() (Context, error) {
	return newContext(e.ID, newGraph(e.Steps))
}
