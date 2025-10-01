package executor

import (
	"context"
	"exesh/internal/domain/execution"
	"fmt"
	"time"
)

type (
	JobExecutor struct {
		executors []jobExecutor
	}

	jobExecutor interface {
		SupportsType(execution.JobType) bool
		Execute(context.Context, execution.Job) execution.Result
	}
)

func NewJobExecutor(executors ...jobExecutor) *JobExecutor {
	return &JobExecutor{executors: executors}
}

func (e *JobExecutor) Execute(ctx context.Context, job execution.Job) execution.Result {
	for _, executor := range e.executors {
		if executor.SupportsType(job.GetType()) {
			return executor.Execute(ctx, job)
		}
	}
	return execution.ResultDetails{
		ID:     job.GetID(),
		DoneAt: time.Now(),
		Error:  fmt.Errorf("executor for %s job not found", job.GetType()).Error(),
	}
}
