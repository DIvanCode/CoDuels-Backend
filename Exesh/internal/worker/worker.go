package worker

import (
	"context"
	"errors"
	"exesh/internal/api/heartbeat"
	"exesh/internal/config"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
	"exesh/internal/executor"
	"exesh/internal/lib/queue"
	"fmt"
	errs "github.com/DIvanCode/filestorage/pkg/errors"
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

		jobs queue.Queue[jobs.Job]

		sourceProvider sourceProvider

		mu        sync.Mutex
		doneJobs  []results.Result
		freeSlots int
	}

	heartbeatClient interface {
		Heartbeat(context.Context, string, []results.Result, int) ([]jobs.Job, []sources.Source, error)
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

		jobs: *queue.NewQueue[jobs.Job](),

		sourceProvider: sourceProvider,

		mu:        sync.Mutex{},
		doneJobs:  make([]results.Result, 0),
		freeSlots: cfg.FreeSlots,
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

		if w.getFreeSlots() == 0 {
			continue
		}

		w.mu.Lock()

		doneJobs := make([]results.Result, len(w.doneJobs))
		copy(doneJobs, w.doneJobs)
		w.doneJobs = make([]results.Result, 0)

		freeSlots := w.freeSlots - w.jobs.Size()

		w.mu.Unlock()

		jbs, srcs, err := w.heartbeatClient.Heartbeat(ctx, w.cfg.WorkerID, doneJobs, freeSlots)
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
			w.jobs.Enqueue(jb)
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

		w.changeFreeSlots(-1)

		job := w.jobs.Dequeue()
		if job == nil {
			w.changeFreeSlots(+1)
			continue
		}

		jobID := job.GetID()
		w.log.Debug("picked job", slog.String("job", jobID.String()))

		result := w.executeJob(ctx, *job)

		w.mu.Lock()
		w.doneJobs = append(w.doneJobs, result)
		w.mu.Unlock()

		w.log.Debug("done job", slog.String("job", jobID.String()))
		w.changeFreeSlots(+1)
	}
}

func (w *Worker) getFreeSlots() int {
	w.mu.Lock()
	defer w.mu.Unlock()
	return w.freeSlots
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
	err = exec.SaveOutput(ctx)
	if err != nil && !errors.Is(err, errs.ErrFileAlreadyExists) {
		w.log.Error("failed to save output", slog.Any("err", err))
	}

	return result
}
