package executor

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
)

type (
	JobExecutor struct {
		executors []jobExecutor
	}

	jobExecutor interface {
		SupportsType(job.Type) bool
		Execute(context.Context, jobs.Job) results.Result
	}
)

func NewJobExecutor(executors ...jobExecutor) *JobExecutor {
	return &JobExecutor{executors: executors}
}

func (e *JobExecutor) Execute(ctx context.Context, jb jobs.Job) results.Result {
	var res results.Result
	for _, executor := range e.executors {
		if executor.SupportsType(jb.GetType()) {
			res = executor.Execute(ctx, jb)
			break
		}
	}
	return res
}
