package execution

import (
	"exesh/internal/domain/execution/source/sources"
	"time"
)

type (
	Definition struct {
		ID          ID
		Stages      []StageDefinition
		Sources     []sources.Definition
		Status      Status
		CreatedAt   time.Time
		ScheduledAt *time.Time
		FinishedAt  *time.Time
	}

	Status string
)

const (
	StatusNew       Status = "new"
	StatusScheduled Status = "scheduled"
	StatusFinished  Status = "finished"
)

func NewExecutionDefinition(stages []StageDefinition, sources []sources.Definition) Definition {
	return Definition{
		ID:          newID(),
		Stages:      stages,
		Sources:     sources,
		Status:      StatusNew,
		CreatedAt:   time.Now(),
		ScheduledAt: nil,
		FinishedAt:  nil,
	}
}

func (def *Definition) SetScheduled(scheduledAt time.Time) {
	if def.Status == StatusFinished {
		return
	}

	def.Status = StatusScheduled
	def.ScheduledAt = &scheduledAt
}

func (def *Definition) SetFinished(finishedAt time.Time) {
	if def.Status == StatusFinished {
		return
	}

	def.Status = StatusFinished
	def.ScheduledAt = &finishedAt
}
