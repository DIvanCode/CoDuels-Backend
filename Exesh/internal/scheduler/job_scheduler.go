package scheduler

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
	"log/slog"
	"sync"
)

type (
	JobScheduler struct {
		log *slog.Logger

		executionScheduler *ExecutionScheduler

		mu            sync.Mutex
		scheduledJobs map[job.ID]*Job
	}
)

func NewJobScheduler(log *slog.Logger, executionScheduler *ExecutionScheduler) *JobScheduler {
	return &JobScheduler{
		log: log,

		executionScheduler: executionScheduler,

		mu:            sync.Mutex{},
		scheduledJobs: make(map[job.ID]*Job),
	}
}

func (s *JobScheduler) PickJob(ctx context.Context, workerID string) (*jobs.Job, []sources.Source) {
	jbs := s.executionScheduler.pickJobs()
	if len(jbs) == 0 {
		return nil, nil
	}

	jb := jbs[0]

	s.mu.Lock()
	defer s.mu.Unlock()

	s.scheduledJobs[jb.GetID()] = jb
	jb.OnSchedule(ctx)

	return &jb.Job, jb.Sources
}

func (s *JobScheduler) DoneJob(ctx context.Context, workerID string, res results.Result) {
	prepareCallback := func() doneCallback {
		s.mu.Lock()
		defer s.mu.Unlock()

		jobID := res.GetJobID()
		jb, ok := s.scheduledJobs[jobID]
		if !ok {
			return nil
		}

		delete(s.scheduledJobs, jobID)

		return jb.OnDone
	}

	cb := prepareCallback()
	if cb != nil {
		cb(ctx, res)
	}
}
