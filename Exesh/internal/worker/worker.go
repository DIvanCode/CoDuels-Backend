package worker

import (
	"context"
	"encoding/json"
	"exesh/internal/api/heartbeat"
	"exesh/internal/config"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
	"exesh/internal/lib/queue"
	"log/slog"
	"sync"
	"time"
)

type (
	Worker struct {
		log *slog.Logger
		cfg config.WorkConfig

		heartbeatClient heartbeatClient
		jobExecutor     jobExecutor

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

	jobExecutor interface {
		Execute(context.Context, jobs.Job) results.Result
	}
)

func NewWorker(log *slog.Logger, cfg config.WorkConfig, sourceProvider sourceProvider, jobExecutor jobExecutor) *Worker {
	return &Worker{
		log: log,
		cfg: cfg,

		heartbeatClient: heartbeat.NewHeartbeatClient(cfg.CoordinatorEndpoint),
		jobExecutor:     jobExecutor,

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
	for {
		timer := time.NewTicker(w.cfg.HeartbeatDelay)

		select {
		case <-ctx.Done():
			w.log.Info("exit heartbeat loop")
			return
		case <-timer.C:
			break
		}

		if w.getFreeSlots() == 0 {
			w.log.Debug("skip heartbeat loop (no free slots)")
			continue
		}

		w.log.Debug("begin heartbeat loop")

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
	for {
		timer := time.NewTicker(w.cfg.WorkerDelay)

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
			w.log.Debug("skip worker loop (no jobs to do)")
			w.changeFreeSlots(+1)
			continue
		}

		js, _ := json.Marshal(job)
		w.log.Debug("picked job", slog.Any("job_id", (*job).GetID()), slog.String("job", string(js)))

		result := w.jobExecutor.Execute(ctx, *job)

		w.mu.Lock()
		w.doneJobs = append(w.doneJobs, result)
		w.mu.Unlock()

		js, _ = json.Marshal(result)
		w.log.Info("done job", slog.Any("job_id", (*job).GetID()), slog.Any("error", result.GetError()),
			slog.Any("result", js))
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
