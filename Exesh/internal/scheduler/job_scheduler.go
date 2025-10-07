package scheduler

import (
	"context"
	"exesh/internal/domain/execution"
	"exesh/internal/lib/queue"
	"log/slog"
)

type (
	JobScheduler struct {
		log *slog.Logger

		scheduledJobs queue.Queue[execution.Job]
		jobCallbacks  map[execution.JobID]*queue.Queue[jobCallback]
	}

	jobCallback func(context.Context, execution.Result)
)

func NewJobScheduler(log *slog.Logger) *JobScheduler {
	return &JobScheduler{
		log: log,

		scheduledJobs: *queue.NewQueue[execution.Job](),
		jobCallbacks:  make(map[execution.JobID]*queue.Queue[jobCallback]),
	}
}

func (s *JobScheduler) Schedule(ctx context.Context, job execution.Job, onJobDone jobCallback) {
	s.scheduledJobs.Enqueue(job)

	jobCallbacks := s.jobCallbacks[job.GetID()]
	if jobCallbacks == nil {
		jobCallbacks = queue.NewQueue[jobCallback]()
	}
	jobCallbacks.Enqueue(onJobDone)
	s.jobCallbacks[job.GetID()] = jobCallbacks
}

func (s *JobScheduler) PickJob(ctx context.Context, workerID string) *execution.Job {
	return s.scheduledJobs.Dequeue()
}

func (s *JobScheduler) DoneJob(ctx context.Context, workerID string, result execution.Result) {
	jobID := result.GetJobID()

	jobCallbacks := s.jobCallbacks[jobID]
	if jobCallbacks == nil {
		return
	}

	jobCallback := jobCallbacks.Dequeue()
	if jobCallback == nil {
		return
	}

	(*jobCallback)(ctx, result)
}
