package scheduler

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
	"exesh/internal/lib/queue"
	"log/slog"
	"sync"
)

type (
	JobScheduler struct {
		log *slog.Logger

		mu sync.Mutex

		scheduledJobs queue.Queue[jobs.Job]
		jobSources    map[job.ID][]sources.Source
		jobCallback   map[job.ID]jobCallback
	}

	jobCallback func(context.Context, results.Result)
)

func NewJobScheduler(log *slog.Logger) *JobScheduler {
	return &JobScheduler{
		log: log,

		mu: sync.Mutex{},

		scheduledJobs: *queue.NewQueue[jobs.Job](),
		jobSources:    make(map[job.ID][]sources.Source),
		jobCallback:   make(map[job.ID]jobCallback),
	}
}

func (s *JobScheduler) Schedule(ctx context.Context, jb jobs.Job, srcs []sources.Source, onJobDone jobCallback) {
	s.mu.Lock()
	defer s.mu.Unlock()

	s.scheduledJobs.Enqueue(jb)
	s.jobSources[jb.GetID()] = srcs
	s.jobCallback[jb.GetID()] = onJobDone
}

func (s *JobScheduler) PickJob(ctx context.Context, workerID string) (*jobs.Job, []sources.Source) {
	s.mu.Lock()
	defer s.mu.Unlock()

	jb := s.scheduledJobs.Dequeue()
	if jb == nil {
		return jb, nil
	}

	srcs := s.jobSources[jb.GetID()]
	return jb, srcs
}

func (s *JobScheduler) DoneJob(ctx context.Context, workerID string, res results.Result) {
	s.mu.Lock()
	defer s.mu.Unlock()

	jobID := res.GetJobID()

	if _, ok := s.jobSources[jobID]; ok {
		delete(s.jobSources, jobID)
	}
	if callback, ok := s.jobCallback[jobID]; ok {
		delete(s.jobCallback, jobID)
		callback(ctx, res)
	}
}
