package scheduler

import (
	"context"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
)

type (
	Job struct {
		jobs.Job
		Sources []sources.Source

		OnSchedule scheduleCallback
		OnDone     doneCallback
	}

	scheduleCallback func(context.Context)
	doneCallback     func(context.Context, results.Result)
)

func NewJob(jb jobs.Job, sources []sources.Source) *Job {
	return &Job{
		Job:     jb,
		Sources: sources,
	}
}
