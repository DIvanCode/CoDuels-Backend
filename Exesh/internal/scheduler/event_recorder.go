package scheduler

import (
	"context"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/job"
	"time"
)

type EventRecorder interface {
	RecordExecutionEvent(ctx context.Context, event ExecutionEvent)
	RecordJobEvent(ctx context.Context, event JobEvent)
	RecordWorkerEvent(ctx context.Context, event WorkerEvent)
}

type ExecutionEvent struct {
	Type            string
	ExecutionID     execution.ID
	Priority        float64
	ProgressRatio   float64
	DurationSeconds float64
	Status          string
	At              time.Time
}

type JobEvent struct {
	Type                    string
	JobID                   job.ID
	ExecutionID             execution.ID
	WorkerID                string
	JobType                 string
	Status                  string
	ExpectedMemoryMB        int
	ExpectedDurationMillis  int
	MemoryStartMB           int
	MemoryEndMB             int
	PromisedStartAt         *time.Time
	StartedAt               *time.Time
	FinishedAt              *time.Time
	ExpectedFinishedAt      *time.Time
	ActualDurationSeconds   float64
	SchedulerLatencySeconds float64
	At                      time.Time
}

type WorkerEvent struct {
	Type              string
	WorkerID          string
	TotalSlots        int
	TotalMemoryMB     int
	FreeSlots         int
	AvailableMemoryMB int
	RunningJobs       int
	UsedMemoryMB      int
	At                time.Time
}

type NoopEventRecorder struct{}

func (NoopEventRecorder) RecordExecutionEvent(context.Context, ExecutionEvent) {}
func (NoopEventRecorder) RecordJobEvent(context.Context, JobEvent)             {}
func (NoopEventRecorder) RecordWorkerEvent(context.Context, WorkerEvent)       {}
