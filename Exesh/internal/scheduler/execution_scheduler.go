package scheduler

import (
	"context"
	"errors"
	"exesh/internal/config"
	"exesh/internal/domain/execution"
	"fmt"
	"log/slog"
	"sync/atomic"
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

		nowExecutions atomic.Int64
	}

	unitOfWork interface {
		Do(context.Context, func(context.Context) error) error
	}

	executionStorage interface {
		GetForUpdate(context.Context, execution.ID) (*execution.Execution, error)
		GetForSchedule(context.Context, time.Time) (*execution.Execution, error)
		Save(context.Context, execution.Execution) error
	}

	jobFactory interface {
		Create(context.Context, *execution.Context, execution.Step) (execution.Job, error)
	}

	jobScheduler interface {
		Schedule(context.Context, execution.Job, jobCallback)
	}

	messageFactory interface {
		CreateExecutionStarted(*execution.Context) (execution.Message, error)
		CreateForStep(*execution.Context, execution.Step, execution.Result) (execution.Message, error)
		CreateExecutionFinished(*execution.Context) (execution.Message, error)
		CreateExecutionFinishedError(*execution.Context, string) (execution.Message, error)
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

		nowExecutions: atomic.Int64{},
	}
}

func (s *ExecutionScheduler) Start(ctx context.Context) {
	go func() {
		err := s.runExecutionScheduler(ctx)
		if errors.Is(err, context.Canceled) {
			err = nil
		}
		if err != nil {
			s.log.Error("execution scheduler exited with error", slog.Any("error", err))
		}
	}()
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

		if s.GetNowExecutions() == s.cfg.MaxConcurrency {
			s.log.Info("skip execution scheduler loop (max concurrency reached)")
			continue
		}

		s.log.Info("begin execution scheduler loop", slog.Int("now_executions", s.GetNowExecutions()))

		s.changeNowExecutions(+1)
		if err := s.unitOfWork.Do(ctx, func(ctx context.Context) error {
			e, err := s.executionStorage.GetForSchedule(ctx, time.Now().Add(-s.cfg.ExecutionRetryAfter))
			if err != nil {
				return fmt.Errorf("failed to get execution for schedule from storage: %w", err)
			}
			if e == nil {
				s.changeNowExecutions(-1)
				s.log.Info("no executions to schedule")
				return nil
			}

			e.SetScheduled(time.Now())

			execCtx, err := e.BuildContext()
			if err != nil {
				return fmt.Errorf("failed to build execution context: %w", err)
			}
			if err = s.scheduleExecution(ctx, &execCtx); err != nil {
				return fmt.Errorf("failed to schedule execution: %w", err)
			}

			if err = s.executionStorage.Save(ctx, *e); err != nil {
				return fmt.Errorf("failed to update execution in storage %s: %w", e.ID.String(), err)
			}

			return nil
		}); err != nil {
			s.changeNowExecutions(-1)
			s.log.Error("failed to schedule execution", slog.Any("error", err))
		}
	}
}

func (s *ExecutionScheduler) scheduleExecution(ctx context.Context, execCtx *execution.Context) error {
	s.log.Info("schedule execution", slog.String("execution_id", execCtx.ExecutionID.String()))

	msg, err := s.messageFactory.CreateExecutionStarted(execCtx)
	if err != nil {
		return fmt.Errorf("failed to create execution started message: %w", err)
	}
	if err = s.messageSender.Send(ctx, msg); err != nil {
		return fmt.Errorf("failed to send %s message: %w", msg.GetType(), err)
	}

	for _, step := range execCtx.PickSteps() {
		if err = s.scheduleStep(ctx, execCtx, step); err != nil {
			return err
		}
	}

	return nil
}

func (s *ExecutionScheduler) scheduleStep(
	ctx context.Context,
	execCtx *execution.Context,
	step execution.Step,
) error {
	if execCtx.IsDone() {
		return nil
	}

	s.log.Info("schedule step", slog.Any("step", step.GetName()))

	job, err := s.jobFactory.Create(ctx, execCtx, step)
	if err != nil {
		s.log.Error("failed to create job for step", slog.Any("step_name", step.GetName()), slog.Any("error", err))
		return fmt.Errorf("failed to create job for step %s: %w", step.GetName(), err)
	}

	s.jobScheduler.Schedule(ctx, job, func(ctx context.Context, result execution.Result) {
		if result.GetError() != nil {
			s.failStep(ctx, execCtx, step, result)
		} else {
			s.doneStep(ctx, execCtx, step, result)
		}
	})

	execCtx.ScheduledStep(step, job)

	return nil
}

func (s *ExecutionScheduler) failStep(
	ctx context.Context,
	execCtx *execution.Context,
	step execution.Step,
	result execution.Result,
) {
	if execCtx.IsDone() {
		return
	}

	s.log.Info("fail step",
		slog.Any("step", step.GetName()),
		slog.Any("execution", execCtx.ExecutionID.String()),
		slog.Any("error", result.GetError()),
	)

	s.finishExecution(ctx, execCtx, result.GetError())
}

func (s *ExecutionScheduler) doneStep(
	ctx context.Context,
	execCtx *execution.Context,
	step execution.Step,
	result execution.Result,
) {
	if execCtx.IsDone() {
		return
	}

	s.log.Info("done step",
		slog.Any("step", step.GetName()),
		slog.Any("execution", execCtx.ExecutionID.String()),
	)

	if err := s.unitOfWork.Do(ctx, func(ctx context.Context) error {
		e, err := s.executionStorage.GetForUpdate(ctx, execCtx.ExecutionID)
		if err != nil {
			return fmt.Errorf("failed to get execution for update from storage: %w", err)
		}
		if e == nil {
			return fmt.Errorf("failed to get execution for update from storage: not found")
		}

		msg, err := s.messageFactory.CreateForStep(execCtx, step, result)
		if err != nil {
			return fmt.Errorf("failed to create message for step: %w", err)
		}
		if err = s.messageSender.Send(ctx, msg); err != nil {
			return fmt.Errorf("failed to send message for step: %w", err)
		}

		execCtx.DoneStep(step.GetName())

		e.SetScheduled(time.Now())

		if err = s.executionStorage.Save(ctx, *e); err != nil {
			return err
		}
		return nil
	}); err != nil {
		s.log.Error("failed to update execution in storage for done step", slog.Any("error", err))
		s.finishExecution(
			ctx,
			execCtx,
			fmt.Errorf("failed to update execution in storage for done step %s: %w", step.GetName(), err))
		return
	}

	if execCtx.IsDone() || result.ShouldFinishExecution() {
		s.finishExecution(ctx, execCtx, nil)
		return
	}

	for _, step = range execCtx.PickSteps() {
		if err := s.scheduleStep(ctx, execCtx, step); err != nil {
			s.log.Error("failed to schedule step",
				slog.Any("step", step.GetName()),
				slog.Any("error", err))
			s.finishExecution(ctx, execCtx, fmt.Errorf("failed to schedule step %s: %w", step.GetName(), err))
		}
	}
}

func (s *ExecutionScheduler) GetNowExecutions() int {
	return int(s.nowExecutions.Load())
}

func (s *ExecutionScheduler) changeNowExecutions(delta int) {
	s.nowExecutions.Add(int64(delta))
}

func (s *ExecutionScheduler) finishExecution(
	ctx context.Context,
	execCtx *execution.Context,
	execError error,
) {
	if execCtx.IsForceDone() {
		return
	}

	if execError == nil {
		s.log.Info("finish execution", slog.String("execution", execCtx.ExecutionID.String()))
	} else {
		s.log.Warn("finish execution with error",
			slog.String("execution", execCtx.ExecutionID.String()),
			slog.Any("error", execError))
	}

	defer s.changeNowExecutions(-1)

	execCtx.ForceDone()

	if err := s.unitOfWork.Do(ctx, func(ctx context.Context) error {
		e, err := s.executionStorage.GetForUpdate(ctx, execCtx.ExecutionID)
		if err != nil {
			return fmt.Errorf("failed to get execution for update from storage: %w", err)
		}
		if e == nil {
			return fmt.Errorf("failed to get execution for update from storage: not found")
		}

		var msg execution.Message
		if execError == nil {
			msg, err = s.messageFactory.CreateExecutionFinished(execCtx)
		} else {
			msg, err = s.messageFactory.CreateExecutionFinishedError(execCtx, execError.Error())
		}
		if err != nil {
			return fmt.Errorf("failed to create execution finished message: %w", err)
		}
		if err = s.messageSender.Send(ctx, msg); err != nil {
			return fmt.Errorf("failed to send execution finished message: %w", err)
		}

		e.SetFinished(time.Now())

		if err = s.executionStorage.Save(ctx, *e); err != nil {
			return err
		}
		return nil
	}); err != nil {
		s.log.Error("failed to finish execution in storage", slog.Any("error", err))
		return
	}
}
