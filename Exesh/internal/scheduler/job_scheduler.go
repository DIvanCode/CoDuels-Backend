package scheduler

import (
	"context"
	"exesh/internal/domain/execution"
	"log/slog"
)

type JobScheduler struct {
	log *slog.Logger
}

func NewJobScheduler(log *slog.Logger) *JobScheduler {
	return &JobScheduler{
		log: log,
	}
}

func (s *JobScheduler) Schedule(ctx context.Context, job execution.Job, onJobDone func(context.Context, execution.Result)) {
}
