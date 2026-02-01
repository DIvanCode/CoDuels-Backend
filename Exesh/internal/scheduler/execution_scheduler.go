package scheduler

import (
	"context"
	"errors"
	"exesh/internal/config"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/message/messages"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
	"fmt"
	"github.com/DIvanCode/filestorage/pkg/bucket"
	"log/slog"
	"sync/atomic"
	"time"

	"github.com/prometheus/client_golang/prometheus"
)

type (
	ExecutionScheduler struct {
		log *slog.Logger
		cfg config.ExecutionSchedulerConfig

		unitOfWork       unitOfWork
		executionStorage executionStorage

		executionFactory executionFactory
		artifactRegistry artifactRegistry

		jobScheduler jobScheduler

		messageFactory messageFactory
		messageSender  messageSender

		nowExecutions atomic.Int64

		nowExecutionsGauge prometheus.Collector
	}

	unitOfWork interface {
		Do(context.Context, func(context.Context) error) error
	}

	executionStorage interface {
		GetExecutionForUpdate(context.Context, execution.ID) (*execution.Definition, error)
		GetExecutionForSchedule(context.Context, time.Time) (*execution.Definition, error)
		SaveExecution(context.Context, execution.Definition) error
	}

	executionFactory interface {
		Create(context.Context, execution.Definition) (*execution.Execution, error)
	}

	artifactRegistry interface {
		GetWorker(job.ID) (workerID string, err error)
	}

	jobScheduler interface {
		Schedule(context.Context, jobs.Job, []sources.Source, jobCallback)
	}

	messageFactory interface {
		CreateExecutionStarted(execution.ID) messages.Message
		CreateForJob(execution.ID, job.DefinitionName, results.Result) (messages.Message, error)
		CreateExecutionFinished(execution.ID) messages.Message
		CreateExecutionFinishedError(execution.ID, string) messages.Message
	}

	messageSender interface {
		Send(context.Context, messages.Message) error
	}
)

func NewExecutionScheduler(
	log *slog.Logger,
	cfg config.ExecutionSchedulerConfig,
	unitOfWork unitOfWork,
	executionStorage executionStorage,
	executionFactory executionFactory,
	artifactRegistry artifactRegistry,
	jobScheduler jobScheduler,
	messageFactory messageFactory,
	messageSender messageSender,
) *ExecutionScheduler {
	s := &ExecutionScheduler{
		log: log,
		cfg: cfg,

		unitOfWork:       unitOfWork,
		executionStorage: executionStorage,

		executionFactory: executionFactory,
		artifactRegistry: artifactRegistry,

		jobScheduler: jobScheduler,

		messageFactory: messageFactory,
		messageSender:  messageSender,

		nowExecutions: atomic.Int64{},
	}

	s.nowExecutionsGauge = prometheus.NewGaugeFunc(prometheus.GaugeOpts{
		Name: "now_executions",
		Help: "Count of currently running executions",
	}, func() float64 {
		return float64(s.getNowExecutions())
	})

	return s
}

func (s *ExecutionScheduler) RegisterMetrics(r prometheus.Registerer) error {
	return errors.Join(
		r.Register(s.nowExecutionsGauge),
	)
}

func (s *ExecutionScheduler) Start(ctx context.Context) {
	go s.runExecutionScheduler(ctx)
}

func (s *ExecutionScheduler) runExecutionScheduler(ctx context.Context) {
	for {
		timer := time.NewTicker(s.cfg.ExecutionsInterval)

		select {
		case <-ctx.Done():
			s.log.Info("exit execution scheduler")
			return
		case <-timer.C:
			break
		}

		if s.getNowExecutions() == s.cfg.MaxConcurrency {
			s.log.Debug("skip execution scheduler loop (max concurrency reached)")
			continue
		}

		s.log.Debug("begin execution scheduler loop", slog.Int("now_executions", s.getNowExecutions()))

		s.changeNowExecutions(+1)
		if err := s.unitOfWork.Do(ctx, func(ctx context.Context) error {
			def, err := s.executionStorage.GetExecutionForSchedule(ctx, time.Now().Add(-s.cfg.ExecutionRetryAfter))
			if err != nil {
				return fmt.Errorf("failed to get execution for schedule from storage: %w", err)
			}
			if def == nil {
				s.changeNowExecutions(-1)
				s.log.Debug("no executions to schedule")
				return nil
			}

			ex, err := s.executionFactory.Create(ctx, *def)
			if err != nil {
				return fmt.Errorf("failed to create execution: %w", err)
			}

			ex.SetScheduled(time.Now())

			if err = s.scheduleExecution(ctx, ex); err != nil {
				return fmt.Errorf("failed to schedule execution: %w", err)
			}

			if err = s.executionStorage.SaveExecution(ctx, ex.Definition); err != nil {
				return fmt.Errorf("failed to update execution in storage %s: %w", def.ID.String(), err)
			}

			return nil
		}); err != nil {
			s.changeNowExecutions(-1)
			s.log.Error("failed to schedule execution", slog.Any("error", err))
		}
	}
}

func (s *ExecutionScheduler) scheduleExecution(ctx context.Context, ex *execution.Execution) error {
	s.log.Info("schedule execution", slog.String("execution_id", ex.ID.String()))

	msg := s.messageFactory.CreateExecutionStarted(ex.ID)
	if err := s.messageSender.Send(ctx, msg); err != nil {
		return fmt.Errorf("failed to send execution started message: %w", err)
	}

	for _, jb := range ex.PickJobs() {
		if err := s.scheduleJob(ctx, ex, jb); err != nil {
			return err
		}
	}

	return nil
}

func (s *ExecutionScheduler) scheduleJob(
	ctx context.Context,
	ex *execution.Execution,
	jb jobs.Job,
) error {
	if ex.IsDone() {
		return nil
	}

	s.log.Info("schedule job", slog.Any("id", jb.GetID()))

	srcs := make([]sources.Source, 0)
	for _, in := range jb.GetInputs() {
		if in.Type == input.Artifact {
			var jobID job.ID
			if err := jobID.FromString(in.SourceID.String()); err != nil {
				return fmt.Errorf("failed to convert artifact source name to job id: %w", err)
			}
			var bucketID bucket.ID
			if err := bucketID.FromString(jobID.String()); err != nil {
				return fmt.Errorf("failed to convert artifact id to bucket id: %w", err)
			}
			workerID, err := s.artifactRegistry.GetWorker(jobID)
			if err != nil {
				return fmt.Errorf("failed to get worker for job %s: %w", jobID.String(), err)
			}
			out, ok := ex.OutputByJob[jobID]
			if !ok {
				return fmt.Errorf("failed to find output for job %s", jobID.String())
			}
			file := out.File

			src := sources.NewFilestorageBucketFileSource(in.SourceID, bucketID, workerID, file)
			srcs = append(srcs, src)
			continue
		}

		src, ok := ex.SourceByID[in.SourceID]
		if !ok {
			s.log.Error("failed to find source for job",
				slog.Any("source", in.SourceID),
				slog.Any("job", jb.GetID()),
				slog.Any("execution", ex.ID))
			return fmt.Errorf("failed to find source for job")
		}

		srcs = append(srcs, src)
	}

	s.jobScheduler.Schedule(ctx, jb, srcs, func(ctx context.Context, res results.Result) {
		if res.GetError() != nil {
			s.failJob(ctx, ex, jb, res)
		} else {
			s.doneJob(ctx, ex, jb, res)
		}
	})

	return nil
}

func (s *ExecutionScheduler) failJob(
	ctx context.Context,
	ex *execution.Execution,
	jb jobs.Job,
	res results.Result,
) {
	if ex.IsDone() {
		return
	}

	s.log.Info("fail job",
		slog.Any("job", jb.GetID()),
		slog.Any("execution", ex.ID.String()),
		slog.Any("error", res.GetError()),
	)

	s.finishExecution(ctx, ex, res.GetError())
}

func (s *ExecutionScheduler) doneJob(
	ctx context.Context,
	ex *execution.Execution,
	jb jobs.Job,
	res results.Result,
) {
	if ex.IsDone() {
		return
	}

	s.log.Info("done job",
		slog.Any("job", jb.GetID()),
		slog.Any("execution", ex.ID.String()),
	)

	if err := s.unitOfWork.Do(ctx, func(ctx context.Context) error {
		e, err := s.executionStorage.GetExecutionForUpdate(ctx, ex.ID)
		if err != nil {
			return fmt.Errorf("failed to get execution for update from storage: %w", err)
		}
		if e == nil {
			return fmt.Errorf("failed to get execution for update from storage: not found")
		}

		jobName := ex.JobDefinitionByID[jb.GetID()].GetName()
		msg, err := s.messageFactory.CreateForJob(ex.ID, jobName, res)
		if err != nil {
			return fmt.Errorf("failed to create message for job: %w", err)
		}

		if err = s.messageSender.Send(ctx, msg); err != nil {
			return fmt.Errorf("failed to send message for step: %w", err)
		}

		ex.DoneJob(jb.GetID(), res.GetStatus())

		e.SetScheduled(time.Now())

		if err = s.executionStorage.SaveExecution(ctx, *e); err != nil {
			return err
		}
		return nil
	}); err != nil {
		s.log.Error("failed to update execution in storage for done job", slog.Any("error", err))
		s.finishExecution(ctx, ex,
			fmt.Errorf("failed to update execution in storage for done job %s: %w", jb.GetID(), err))
		return
	}

	if ex.IsDone() {
		s.finishExecution(ctx, ex, nil)
		return
	}

	for _, jb = range ex.PickJobs() {
		if err := s.scheduleJob(ctx, ex, jb); err != nil {
			s.log.Error("failed to schedule job",
				slog.Any("job", jb.GetID()),
				slog.Any("error", err))
			s.finishExecution(ctx, ex, fmt.Errorf("failed to schedule job %s: %w", jb.GetID(), err))
		}
	}
}

func (s *ExecutionScheduler) finishExecution(
	ctx context.Context,
	ex *execution.Execution,
	exError error,
) {
	if ex.IsForceFailed() {
		return
	}

	if exError == nil {
		s.log.Info("finish execution", slog.String("execution", ex.ID.String()))
	} else {
		s.log.Warn("finish execution with error",
			slog.String("execution", ex.ID.String()),
			slog.Any("error", exError))
	}

	defer s.changeNowExecutions(-1)

	ex.ForceFail()

	if err := s.unitOfWork.Do(ctx, func(ctx context.Context) error {
		var msg messages.Message
		if exError == nil {
			msg = s.messageFactory.CreateExecutionFinished(ex.ID)
		} else {
			msg = s.messageFactory.CreateExecutionFinishedError(ex.ID, exError.Error())
		}

		if err := s.messageSender.Send(ctx, msg); err != nil {
			return fmt.Errorf("failed to send execution finished message: %w", err)
		}

		ex.SetFinished(time.Now())

		if err := s.executionStorage.SaveExecution(ctx, ex.Definition); err != nil {
			return err
		}
		return nil
	}); err != nil {
		s.log.Error("failed to finish execution in storage", slog.Any("error", err))
		return
	}
}

func (s *ExecutionScheduler) getNowExecutions() int {
	return int(s.nowExecutions.Load())
}

func (s *ExecutionScheduler) changeNowExecutions(delta int) {
	s.nowExecutions.Add(int64(delta))
}
