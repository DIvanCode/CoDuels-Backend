package scheduler

import (
	"context"
	"exesh/internal/config"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/graph"
	"fmt"
	"log/slog"
	"sync"
	"time"
)

type (
	Scheduler struct {
		log *slog.Logger
		cfg config.SchedulerConfig

		unitOfWork       unitOfWork
		executionStorage executionStorage

		graphFactory graphFactory

		mu            sync.Mutex
		nowExecutions int
	}

	unitOfWork interface {
		Do(context.Context, func(context.Context) error) error
	}

	executionStorage interface {
		GetForSchedule(context.Context, time.Time) (*execution.Execution, error)
		Update(context.Context, execution.Execution) error
	}

	graphFactory interface {
		CreateForExecution(context.Context, execution.Execution) (*graph.Graph, error)
	}
)

func NewScheduler(
	log *slog.Logger,
	cfg config.SchedulerConfig,
	unitOfWork unitOfWork,
	executionStorage executionStorage,
	graphFactory graphFactory,
) *Scheduler {
	return &Scheduler{
		log: log,
		cfg: cfg,

		unitOfWork:       unitOfWork,
		executionStorage: executionStorage,

		graphFactory: graphFactory,

		mu:            sync.Mutex{},
		nowExecutions: 0,
	}
}

func (s *Scheduler) Start(ctx context.Context) {
	go s.runExecutionScheduler(ctx)
}

func (s *Scheduler) runExecutionScheduler(ctx context.Context) error {
	for {
		timer := time.NewTicker(s.cfg.ExecutionsInterval)

		select {
		case <-ctx.Done():
			s.log.Info("exit execution scheduler")
			return ctx.Err()
		case <-timer.C:
			break
		}

		if s.getNowExecutions() == s.cfg.MaxConcurrency {
			s.log.Info("skip execution scheduler loop (max concurrency reached)")
			continue
		}

		s.log.Info("begin execution scheduler loop")

		if err := s.unitOfWork.Do(ctx, func(ctx context.Context) error {
			s.changeNowExecutions(+1)

			e, err := s.executionStorage.GetForSchedule(ctx, time.Now().Add(-s.cfg.ExecutionRetryAfter))
			if err != nil {
				s.changeNowExecutions(-1)
				return fmt.Errorf("failed to get execution for schedule from storage: %w", err)
			}
			if e == nil {
				s.changeNowExecutions(-1)
				s.log.Info("no executions to schedule")
				return nil
			}

			g, err := s.graphFactory.CreateForExecution(ctx, *e)
			if err != nil {
				s.changeNowExecutions(-1)
				return fmt.Errorf("failed to create graph for execution %s: %w", e.ID.String(), err)
			}

			e.SetScheduled(time.Now())

			if err = s.executionStorage.Update(ctx, *e); err != nil {
				s.changeNowExecutions(-1)
				return fmt.Errorf("failed to update execution in storage %s: %w", e.ID.String(), err)
			}

			s.log.Info("scheduled execution", slog.Any("execution_id", e.ID))

			s.scheduleGraph(g)

			return nil
		}); err != nil {
			s.log.Error("failed to schedule execution", slog.Any("error", err))
		}
	}
}

func (s *Scheduler) scheduleGraph(g *graph.Graph) {
	s.log.Info("schedule graph", slog.String("graph_id", g.ID.String()))

	for _, job := range g.PickJobs() {
		s.scheduleJob(job)
	}
}

func (s *Scheduler) scheduleJob(job graph.Job) {
	s.log.Info("schedule job", slog.String("job_id", job.GetID().String()))
}

func (s *Scheduler) getNowExecutions() int {
	s.mu.Lock()
	defer s.mu.Unlock()

	return s.nowExecutions
}

func (s *Scheduler) changeNowExecutions(delta int) {
	s.mu.Lock()
	defer s.mu.Unlock()

	s.nowExecutions += delta
}
