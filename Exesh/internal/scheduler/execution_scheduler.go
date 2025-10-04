package scheduler

import (
	"context"
	"exesh/internal/config"
	"exesh/internal/domain/execution"
	"fmt"
	"log/slog"
	"sync"
	"time"
)

type (
	ExecutionScheduler struct {
		log *slog.Logger
		cfg config.ExecutionSchedulerConfig

		unitOfWork       unitOfWork
		executionStorage executionStorage

		jobFactory   jobFactory
		jobScheduler jobScheduler

		messageFactory messageFactory
		messageSender  messageSender

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

	jobFactory interface {
		Create(context.Context, execution.Context, execution.Step) (execution.Job, error)
	}

	jobScheduler interface {
		Schedule(context.Context, execution.Job, func(context.Context, execution.Result))
	}

	messageFactory interface {
		CreateExecutionStarted(execution.Context) (execution.Message, error)
		CreateForStep(execution.Context, execution.Step, execution.Result) (execution.Message, error)
		CreateExecutionFinished(execution.Context) (execution.Message, error)
	}

	messageSender interface {
		Send(context.Context, execution.Message) error
	}
)

func NewExecutionScheduler(
	log *slog.Logger,
	cfg config.ExecutionSchedulerConfig,
	unitOfWork unitOfWork,
	executionStorage executionStorage,
	jobFactory jobFactory,
	jobScheduler jobScheduler,
	messageFactory messageFactory,
	messageSender messageSender,
) *ExecutionScheduler {
	return &ExecutionScheduler{
		log: log,
		cfg: cfg,

		unitOfWork:       unitOfWork,
		executionStorage: executionStorage,

		jobFactory:   jobFactory,
		jobScheduler: jobScheduler,

		messageFactory: messageFactory,
		messageSender:  messageSender,

		mu:            sync.Mutex{},
		nowExecutions: 0,
	}
}

func (s *ExecutionScheduler) Start(ctx context.Context) {
	go s.runExecutionScheduler(ctx)
}

func (s *ExecutionScheduler) runExecutionScheduler(ctx context.Context) error {
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

			e.SetScheduled(time.Now())

			if err = s.executionStorage.Update(ctx, *e); err != nil {
				s.changeNowExecutions(-1)
				return fmt.Errorf("failed to update execution in storage %s: %w", e.ID.String(), err)
			}

			execCtx, err := e.BuildContext()
			if err != nil {
				s.changeNowExecutions(-1)
				return fmt.Errorf("failed to build execution context: %w", err)
			}
			if err = s.scheduleGraph(ctx, execCtx); err != nil {
				s.changeNowExecutions(-1)
				return fmt.Errorf("failed to schedule graph: %w", err)

			}

			return nil
		}); err != nil {
			s.log.Error("failed to schedule execution", slog.Any("error", err))
		}
	}
}

func (s *ExecutionScheduler) scheduleGraph(ctx context.Context, execCtx execution.Context) error {
	s.log.Info("schedule execution", slog.String("execution_id", execCtx.ExecutionID.String()))

	msg, err := s.messageFactory.CreateExecutionStarted(execCtx)
	if err != nil {
		return fmt.Errorf("failed to create execution started message: %w", err)
	}
	if err = s.messageSender.Send(ctx, msg); err != nil {
		return fmt.Errorf("failed to send %s message: %w", msg.GetType(), err)
	}

	for _, step := range execCtx.PickSteps() {
		s.scheduleStep(ctx, execCtx, step)
	}

	return nil
}

func (s *ExecutionScheduler) scheduleStep(ctx context.Context, execCtx execution.Context, step execution.Step) {
	s.log.Info("schedule step", slog.Any("step_name", step.GetName()))

	job, err := s.jobFactory.Create(ctx, execCtx, step)
	if err != nil {
		s.log.Error("failed to create job for step", slog.Any("step_name", step.GetName()), slog.Any("error", err))
		return
	}

	s.jobScheduler.Schedule(ctx, job, func(ctx context.Context, result execution.Result) {
		msg, err := s.messageFactory.CreateForStep(execCtx, step, result)
		if err != nil {
			s.log.Error("failed to create message for result", slog.Any("step_name", step.GetName()), slog.Any("error", err))
			return
		}
		if err = s.messageSender.Send(ctx, msg); err != nil {
			s.log.Error("failed to send message", slog.Any("step_name", step.GetName()), slog.Any("error", err))
			return
		}

		if err = s.doneStep(ctx, execCtx, step); err != nil {
			s.log.Error("failed to done step", slog.Any("step_name", step.GetName()), slog.Any("error", err))
			return
		}
	})

	execCtx.ScheduledStep(step, job)
}

func (s *ExecutionScheduler) doneStep(ctx context.Context, execCtx execution.Context, step execution.Step) error {
	execCtx.DoneStep(step.GetName())

	if execCtx.IsDone() {
		msg, err := s.messageFactory.CreateExecutionFinished(execCtx)
		if err != nil {
			return fmt.Errorf("failed to create execution finished message: %w", err)
		}
		if err = s.messageSender.Send(ctx, msg); err != nil {
			return fmt.Errorf("failed to send %s message: %w", msg.GetType(), err)
		}
		return nil
	}

	for _, step := range execCtx.PickSteps() {
		s.scheduleStep(ctx, execCtx, step)
	}

	return nil
}

func (s *ExecutionScheduler) getNowExecutions() int {
	s.mu.Lock()
	defer s.mu.Unlock()

	return s.nowExecutions
}

func (s *ExecutionScheduler) changeNowExecutions(delta int) {
	s.mu.Lock()
	defer s.mu.Unlock()

	s.nowExecutions += delta
}
