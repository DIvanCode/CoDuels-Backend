package worker

import (
	"context"
	"encoding/json"
	"exesh/internal/api/heartbeat"
	"exesh/internal/config"
	"exesh/internal/domain/execution"
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

		jobs queue.Queue[execution.Job]

		mu        sync.Mutex
		doneJobs  []execution.Result
		freeSlots int
	}

	heartbeatClient interface {
		Heartbeat(context.Context, string, []execution.Result, int) ([]execution.Job, error)
	}

	jobExecutor interface {
		Execute(context.Context, execution.Job) execution.Result
	}
)

func NewWorker(log *slog.Logger, cfg config.WorkConfig, jobExecutor jobExecutor) *Worker {
	return &Worker{
		log: log,
		cfg: cfg,

		heartbeatClient: heartbeat.NewHeartbeatClient(cfg.CoordinatorEndpoint),
		jobExecutor:     jobExecutor,

		jobs: *queue.NewQueue[execution.Job](),

		mu:        sync.Mutex{},
		doneJobs:  make([]execution.Result, 0),
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
			continue
		}

		w.mu.Lock()

		doneJobs := make([]execution.Result, len(w.doneJobs))
		copy(doneJobs, w.doneJobs)
		w.doneJobs = make([]execution.Result, 0)

		freeSlots := w.freeSlots - w.jobs.Size()

		w.mu.Unlock()

		jobs, err := w.heartbeatClient.Heartbeat(ctx, w.cfg.WorkerID, doneJobs, freeSlots)
		if err != nil {
			w.log.Error("failed to do heartbeat request", slog.Any("err", err))

			w.mu.Lock()
			w.doneJobs = append(w.doneJobs, doneJobs...)
			w.mu.Unlock()

			continue
		}

		for _, job := range jobs {
			w.jobs.Enqueue(job)
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
			w.changeFreeSlots(+1)
			continue
		}

		js, _ := json.Marshal(job)
		w.log.Info("picked job", slog.Any("job_id", (*job).GetID()), slog.String("job", string(js)))

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
