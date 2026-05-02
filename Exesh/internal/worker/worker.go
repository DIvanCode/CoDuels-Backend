package worker

import (
	"context"
	"exesh/internal/api/heartbeat"
	"exesh/internal/config"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
	"exesh/internal/executor"
	"exesh/internal/lib/queue"
	"fmt"
	"log/slog"
	"sync"
	"time"
)

type (
	Worker struct {
		log *slog.Logger
		cfg config.WorkConfig

		heartbeatClient heartbeatClient
		executorFactory *executor.ExecutorFactory

		metrics *workerRuntimeMetrics

		jobs                    queue.Queue[jobs.Job]
		jobsExpectedTotalMemory int

		sourceProvider sourceProvider

		mu              sync.Mutex
		doneJobs        []results.Result
		freeSlots       int
		availableMemory int
	}

	heartbeatClient interface {
		Heartbeat(context.Context, string, []results.Result, int, int, int, int) ([]jobs.Job, []sources.Source, error)
	}

	sourceProvider interface {
		SaveSource(ctx context.Context, src sources.Source) error
		RemoveSource(ctx context.Context, src sources.Source)
	}
)

func NewWorker(
	log *slog.Logger,
	cfg config.WorkConfig,
	sourceProvider sourceProvider,
	executorFactory *executor.ExecutorFactory) *Worker {
	return &Worker{
		log: log,
		cfg: cfg,

		heartbeatClient: heartbeat.NewHeartbeatClient(cfg.CoordinatorEndpoint),
		executorFactory: executorFactory,
		metrics:         newWorkerRuntimeMetrics(cfg.WorkerID),

		jobs:                    *queue.NewQueue[jobs.Job](),
		jobsExpectedTotalMemory: 0,

		sourceProvider: sourceProvider,

		mu:              sync.Mutex{},
		doneJobs:        make([]results.Result, 0),
		freeSlots:       cfg.FreeSlots,
		availableMemory: cfg.AvailableMemory,
	}
}

func (w *Worker) Start(ctx context.Context) {
	go w.runHeartbeat(ctx)
	for range w.cfg.FreeSlots {
		go w.runWorker(ctx)
	}
}

func (w *Worker) runHeartbeat(ctx context.Context) {
	timer := time.NewTicker(w.cfg.HeartbeatDelay)
	defer timer.Stop()

	for {
		select {
		case <-ctx.Done():
			w.log.Info("exit heartbeat loop")
			return
		case <-timer.C:
			break
		}

		w.mu.Lock()

		doneJobs := make([]results.Result, len(w.doneJobs))
		copy(doneJobs, w.doneJobs)
		w.doneJobs = make([]results.Result, 0)

		freeSlots := w.freeSlots - w.jobs.Size()
		availableMemory := w.availableMemory - w.jobsExpectedTotalMemory

		w.mu.Unlock()

		jbs, srcs, err := w.heartbeatClient.Heartbeat(
			ctx,
			w.cfg.WorkerID,
			doneJobs,
			w.cfg.FreeSlots,
			w.cfg.AvailableMemory,
			freeSlots,
			availableMemory,
		)
		if err != nil {
			w.log.Error("failed to do heartbeat request", slog.Any("err", err))

			w.mu.Lock()
			w.doneJobs = append(w.doneJobs, doneJobs...)
			w.mu.Unlock()

			continue
		}

		for _, src := range srcs {
			if err := w.sourceProvider.SaveSource(ctx, src); err != nil {
				w.log.Error("failed to create source", slog.Any("err", err))
			}
		}

		for _, jb := range jbs {
			w.mu.Lock()
			w.jobs.Enqueue(jb)
			w.jobsExpectedTotalMemory += jb.GetExpectedMemory()
			w.mu.Unlock()
		}
	}
}

func (w *Worker) runWorker(ctx context.Context) {
	timer := time.NewTicker(w.cfg.WorkerDelay)
	defer timer.Stop()

	for {
		select {
		case <-ctx.Done():
			w.log.Info("exit worker")
			return
		case <-timer.C:
			break
		}

		w.mu.Lock()

		job := w.jobs.Dequeue()
		if job == nil {
			w.mu.Unlock()
			continue
		}

		w.freeSlots -= 1
		w.jobsExpectedTotalMemory -= job.GetExpectedMemory()
		w.availableMemory -= job.GetExpectedMemory()

		w.mu.Unlock()

		jobID := job.GetID()
		w.log.Debug("picked job", slog.String("job", jobID.String()))

		startedAt := time.Now()
		w.metrics.jobStarted()
		result := w.executeJob(ctx, *job)
		w.metrics.jobFinished(string(result.GetStatus()), time.Since(startedAt), job.GetExpectedMemory())

		w.mu.Lock()

		w.doneJobs = append(w.doneJobs, result)
		w.freeSlots += 1
		w.availableMemory += job.GetExpectedMemory()

		w.mu.Unlock()

		w.log.Debug("done job", slog.String("job", jobID.String()))
	}
}

func (w *Worker) changeFreeSlots(delta int) {
	w.mu.Lock()
	defer w.mu.Unlock()
	w.freeSlots += delta
}

func (w *Worker) executeJob(ctx context.Context, jb jobs.Job) results.Result {
	exec, err := w.executorFactory.Create(jb)
	if err != nil {
		return results.Error(jb, fmt.Errorf("create job executor: %w", err))
	}

	jobID := jb.GetID()

	defer func() {
		if stopErr := exec.Stop(ctx); stopErr != nil {
			w.log.Error(
				"failed to stop job executor",
				slog.String("job", jobID.String()),
				slog.Any("err", stopErr),
			)
		}
	}()

	if err = exec.Init(ctx); err != nil {
		return results.Error(jb, fmt.Errorf("init job executor: %w", err))
	}
	if err = exec.PrepareInput(ctx); err != nil {
		return results.Error(jb, fmt.Errorf("prepare input: %w", err))
	}
	result := exec.ExecuteCommand(ctx)

	if !result.GetHasOutput() {
		return result
	}

	err = exec.SaveOutput(ctx, &result)
	if err != nil {
		w.log.Error("failed to save output", slog.Any("err", err))
	}

	return result
}
