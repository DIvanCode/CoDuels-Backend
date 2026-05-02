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
	"exesh/internal/domain/execution/result"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
	"fmt"
	"log/slog"
	"sort"
	"sync"
	"sync/atomic"
	"time"

	"github.com/DIvanCode/filestorage/pkg/bucket"

	"github.com/prometheus/client_golang/prometheus"
)

type (
	ExecutionScheduler struct {
		log *slog.Logger
		cfg config.ExecutionSchedulerConfig

		unitOfWork       unitOfWork
		executionStorage executionStorage
		categoryStats    categoryStats

		executionFactory executionFactory
		workerPool       *WorkerPool

		messageFactory    messageFactory
		messageDispatcher messageDispatcher

		nowWeight      atomic.Int64
		nowWeightGauge prometheus.Collector
		metrics        *executionAggregateMetrics

		mu         sync.Mutex
		executions map[execution.ID]*Execution
	}

	unitOfWork interface {
		Do(context.Context, func(context.Context) error) error
	}

	executionStorage interface {
		GetExecutionForUpdate(context.Context, execution.ID) (*execution.Definition, error)
		GetExecutionForSchedule(context.Context, time.Time) (*execution.Definition, error)
		SaveExecution(context.Context, execution.Definition) error
	}

	categoryStats interface {
		UpdateCategoryHistogram(context.Context, string, int, int) error
	}

	executionFactory interface {
		Create(context.Context, execution.Definition) (*execution.Execution, error)
	}

	messageFactory interface {
		CreateExecutionStarted(execution.ID) messages.Message
		CreateForJob(execution.ID, job.DefinitionName, results.Result) (messages.Message, error)
		CreateExecutionFinished(execution.ID) messages.Message
		CreateExecutionFinishedError(execution.ID, string) messages.Message
	}

	messageDispatcher interface {
		Send(context.Context, messages.Message) error
	}
)

func NewExecutionScheduler(
	log *slog.Logger,
	cfg config.ExecutionSchedulerConfig,
	unitOfWork unitOfWork,
	executionStorage executionStorage,
	categoryStats categoryStats,
	executionFactory executionFactory,
	workerPool *WorkerPool,
	messageFactory messageFactory,
	messageDispatcher messageDispatcher,
) *ExecutionScheduler {
	s := &ExecutionScheduler{
		log: log,
		cfg: cfg,

		unitOfWork:       unitOfWork,
		executionStorage: executionStorage,
		categoryStats:    categoryStats,

		executionFactory: executionFactory,
		workerPool:       workerPool,

		messageFactory:    messageFactory,
		messageDispatcher: messageDispatcher,

		nowWeight: atomic.Int64{},
		metrics:   newExecutionAggregateMetrics(),

		mu:         sync.Mutex{},
		executions: make(map[execution.ID]*Execution),
	}

	s.nowWeightGauge = prometheus.NewGaugeFunc(prometheus.GaugeOpts{
		Name: "now_weight",
		Help: "Current total weight of running executions",
	}, func() float64 {
		return float64(s.nowWeight.Load())
	})

	return s
}

func (s *ExecutionScheduler) RegisterMetrics(r prometheus.Registerer) error {
	return errors.Join(
		r.Register(s.nowWeightGauge),
		r.Register(newExecutionSchedulerCollector(s)),
		s.metrics.Register(r),
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

		remainingCapacity := s.cfg.Capacity - s.nowWeight.Load()
		if remainingCapacity <= 0 {
			s.log.Debug("skip execution scheduler loop (capacity reached)")
			continue
		}

		nowWeight := s.nowWeight.Load()
		if nowWeight > 0 {
			s.log.Debug(
				"begin execution scheduler loop",
				slog.Int64("now_weight", nowWeight),
				slog.Int64("remaining_capacity", remainingCapacity),
			)
		}

		weight := int64(0)
		if err := s.unitOfWork.Do(ctx, func(ctx context.Context) error {
			def, err := s.executionStorage.GetExecutionForSchedule(ctx, time.Now().Add(-s.cfg.ExecutionRetryAfter))
			if err != nil {
				return fmt.Errorf("failed to get execution for schedule from storage: %w", err)
			}
			if def == nil {
				return nil
			}
			if def.Weight > remainingCapacity {
				s.log.Debug(
					"skip picked execution due to capacity",
					slog.String("execution_id", def.ID.String()),
					slog.Int64("weight", def.Weight),
					slog.Int64("remaining_capacity", remainingCapacity),
				)
				return nil
			}

			innerEx, err := s.executionFactory.Create(ctx, *def)
			if err != nil {
				return fmt.Errorf("failed to create execution: %w", err)
			}

			ex := NewExecution(innerEx)
			func() {
				s.mu.Lock()
				defer s.mu.Unlock()
				s.executions[ex.ID] = ex
			}()

			ex.SetScheduled(time.Now())
			s.nowWeight.Add(+ex.Definition.Weight)
			weight = ex.Definition.Weight

			if err = s.scheduleExecution(ctx, ex); err != nil {
				return fmt.Errorf("failed to schedule execution: %w", err)
			}

			if err = s.executionStorage.SaveExecution(ctx, ex.Definition); err != nil {
				return fmt.Errorf("failed to update execution in storage %s: %w", def.ID.String(), err)
			}

			return nil
		}); err != nil {
			s.nowWeight.Add(-weight)
			s.log.Error("failed to schedule execution", slog.Any("error", err))
		}
	}
}

func (s *ExecutionScheduler) scheduleExecution(ctx context.Context, ex *Execution) error {
	s.log.Info("schedule execution", slog.String("execution_id", ex.ID.String()))
	s.metrics.executionStarted()

	msg := s.messageFactory.CreateExecutionStarted(ex.ID)
	if err := s.messageDispatcher.Send(ctx, msg); err != nil {
		return fmt.Errorf("failed to send execution started message: %w", err)
	}

	for _, jb := range ex.PickJobs() {
		if err := s.scheduleJob(ex, jb); err != nil {
			return err
		}
	}

	return nil
}

func (s *ExecutionScheduler) scheduleJob(ex *Execution, jb jobs.Job) error {
	if ex.IsDone() {
		return nil
	}

	jobID := jb.GetID()
	logArgs := []any{
		slog.String("job", jobID.String()),
		slog.String("type", string(jb.GetType())),
	}
	if jb.GetType() == job.Chain {
		logArgs = append(logArgs, slog.Int("chain_jobs", len(jb.AsChain().Jobs)))
	}
	s.log.Info("schedule job", logArgs...)

	scheduledJob := &Job{Job: jb, ExecutionID: ex.ID}
	scheduledJob.Sources = func(ctx context.Context) ([]sources.Source, error) {
		srcs := make([]sources.Source, 0)
		for _, in := range jb.GetInputs() {
			if in.Type == input.Artifact {
				var inputJobID job.ID
				if err := inputJobID.FromString(in.SourceID.String()); err != nil {
					return nil, fmt.Errorf("failed to convert artifact source name to job id: %w", err)
				}
				var bucketID bucket.ID
				if err := bucketID.FromString(inputJobID.String()); err != nil {
					return nil, fmt.Errorf("failed to convert artifact id to bucket id: %w", err)
				}
				workerID, err := s.workerPool.getWorkerWithArtifact(inputJobID)
				if err != nil {
					return nil, fmt.Errorf("failed to get worker for job %s: %w", inputJobID.String(), err)
				}
				out, ok := ex.OutputByJob[inputJobID]
				if !ok {
					return nil, fmt.Errorf("failed to find output for job %s", inputJobID.String())
				}
				file := out.File

				src := sources.NewFilestorageBucketFileSource(in.SourceID, bucketID, workerID, file)
				srcs = append(srcs, src)
				continue
			}

			src, ok := ex.SourceByID[in.SourceID]
			if !ok {
				inputJobID := jb.GetID()
				s.log.Error("failed to find source for job",
					slog.String("source", in.SourceID.String()),
					slog.String("job", inputJobID.String()),
					slog.String("execution", ex.ID.String()))
				return nil, fmt.Errorf("failed to find source for job")
			}

			srcs = append(srcs, src)
		}
		return srcs, nil
	}
	scheduledJob.OnStart = func(ctx context.Context) {
		s.mu.Lock()
		defer s.mu.Unlock()
		ex.DequeueJob(scheduledJob)
	}
	scheduledJob.OnDone = func(ctx context.Context, res results.Result) {
		if res.GetError() != nil {
			s.failJob(ctx, ex, jb, res)
		} else {
			s.doneJob(ctx, ex, jb, res)
		}
	}

	ex.EnqueueJob(scheduledJob)

	return nil
}

func (s *ExecutionScheduler) pickJobs() []*Job {
	executions := func() []*Execution {
		s.mu.Lock()
		defer s.mu.Unlock()

		exs := make([]*Execution, 0, len(s.executions))
		for _, ex := range s.executions {
			exs = append(exs, ex)
		}

		return exs
	}()

	now := time.Now()
	priorities := make(map[execution.ID]float64, len(executions))
	for i := range executions {
		priorities[executions[i].ID] = executions[i].GetPriority(now)
	}
	sort.Slice(executions, func(i, j int) bool {
		return priorities[executions[i].ID] > priorities[executions[j].ID]
	})

	jbs := make([]*Job, 0)
	for i := range executions {
		jb := executions[i].GetPeekJob()
		if jb != nil {
			s.metrics.executionPick(priorities[executions[i].ID], executions[i].GetProgressRatio())
			jbs = append(jbs, jb)
		}
	}

	return jbs
}

func (s *ExecutionScheduler) failJob(
	ctx context.Context,
	ex *Execution,
	jb jobs.Job,
	res results.Result,
) {
	if ex.IsDone() {
		return
	}

	ex.TotalDoneJobsExpectedTime += int64(jb.GetExpectedTime())

	jobID := jb.GetID()
	s.log.Info("fail job",
		slog.String("job", jobID.String()),
		slog.String("execution", ex.ID.String()),
		slog.Any("error", res.GetError()),
	)

	s.finishExecution(ctx, ex, res.GetError())
}

func (s *ExecutionScheduler) doneJob(ctx context.Context, ex *Execution, jb jobs.Job, res results.Result) {
	if ex.IsDone() {
		return
	}

	ex.TotalDoneJobsExpectedTime += int64(jb.GetExpectedTime())

	jobID := jb.GetID()
	s.log.Info("done job",
		slog.String("job", jobID.String()),
		slog.String("execution", ex.ID.String()),
	)

	if err := s.unitOfWork.Do(ctx, func(ctx context.Context) error {
		e, err := s.executionStorage.GetExecutionForUpdate(ctx, ex.ID)
		if err != nil {
			return fmt.Errorf("failed to get execution for update from storage: %w", err)
		}
		if e == nil {
			return fmt.Errorf("failed to get execution for update from storage: not found")
		}

		jobResults := []results.Result{res}
		if res.GetType() == result.Chain {
			jobResults = res.AsChain().Results
		}

		for _, jobRes := range jobResults {
			jobDef, ok := ex.JobDefinitionByID[jobRes.GetJobID()]
			if !ok {
				jobResID := jobRes.GetJobID()
				return fmt.Errorf("failed to find job definition by id: %s", jobResID.String())
			}

			if s.categoryStats != nil {
				if err = s.categoryStats.UpdateCategoryHistogram(
					ctx,
					jobDef.GetCategoryName(),
					jobRes.GetElapsedTime(),
					jobRes.GetUsedMemory(),
				); err != nil {
					return fmt.Errorf("failed to update category histogram: %w", err)
				}
			}

			msg, msgErr := s.messageFactory.CreateForJob(ex.ID, jobDef.GetName(), jobRes)
			if msgErr != nil {
				return fmt.Errorf("failed to create message for job: %w", msgErr)
			}

			if err = s.messageDispatcher.Send(ctx, msg); err != nil {
				return fmt.Errorf("failed to send message for step: %w", err)
			}
		}

		ex.DoneJob(jobID, res.GetStatus())

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

	for _, pickedJob := range ex.PickJobs() {
		if err := s.scheduleJob(ex, pickedJob); err != nil {
			pickedJobID := pickedJob.GetID()
			s.log.Error("failed to schedule job",
				slog.String("job", pickedJobID.String()),
				slog.Any("error", err))
			s.finishExecution(ctx, ex, fmt.Errorf("failed to schedule job %s: %w", pickedJobID, err))
		}
	}

	if ex.IsDone() {
		s.finishExecution(ctx, ex, nil)
	}
}

func (s *ExecutionScheduler) finishExecution(ctx context.Context, ex *Execution, exError error) {
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
	finishStatus := "ok"
	if exError != nil {
		finishStatus = "error"
	}
	s.metrics.executionFinished(finishStatus, ex.GetDuration(time.Now()), ex.GetProgressRatio())

	defer s.nowWeight.Add(-ex.Definition.Weight)

	ex.ForceFail()
	func() {
		s.mu.Lock()
		defer s.mu.Unlock()
		delete(s.executions, ex.ID)
	}()

	if err := s.unitOfWork.Do(ctx, func(ctx context.Context) error {
		var msg messages.Message
		if exError == nil {
			msg = s.messageFactory.CreateExecutionFinished(ex.ID)
		} else {
			msg = s.messageFactory.CreateExecutionFinishedError(ex.ID, exError.Error())
		}

		if err := s.messageDispatcher.Send(ctx, msg); err != nil {
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
