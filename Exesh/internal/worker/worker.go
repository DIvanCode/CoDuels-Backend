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
		InitExecutor(jobs.Job) (executor.Executor, error)
		ErrorResult(jobs.Job, error) results.Result
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

		jobID := job.GetID()
		w.log.Debug("picked job", slog.String("job", jobID.String()))

		exec, err := w.jobExecutor.InitExecutor(*job)
		if err != nil {
			result := w.jobExecutor.ErrorResult(*job, fmt.Errorf("failed to init executor: %w", err))
			w.mu.Lock()
			w.doneJobs = append(w.doneJobs, result)
			w.mu.Unlock()
			w.log.Debug("done job", slog.String("job", jobID.String()))
			w.changeFreeSlots(+1)
			continue
		}

		err = exec.PrepareInput(ctx)
		if err != nil {
			result := exec.ErrorResult(fmt.Errorf("failed to prepare input: %w", err))
			_ = exec.StopExecutor(ctx)
			w.mu.Lock()
			w.doneJobs = append(w.doneJobs, result)
			w.mu.Unlock()
			w.log.Debug("done job", slog.String("job", jobID.String()))
			w.changeFreeSlots(+1)
			continue
		}
		err = exec.PrepareOutput(ctx)
		if err != nil {
			result := exec.ErrorResult(fmt.Errorf("failed to prepare output: %w", err))
			_ = exec.StopExecutor(ctx)
			w.mu.Lock()
			w.doneJobs = append(w.doneJobs, result)
			w.mu.Unlock()
			w.log.Debug("done job", slog.String("job", jobID.String()))
			w.changeFreeSlots(+1)
			continue
		}
		resultsCh := make(chan results.Result, 16)
		var readWG sync.WaitGroup
		readWG.Add(1)
		go func() {
			defer readWG.Done()
			for range resultsCh {
			}
		}()

		result := exec.Execute(ctx, resultsCh)
		close(resultsCh)
		readWG.Wait()
		if err := exec.StopExecutor(ctx); err != nil {
			result = exec.ErrorResult(fmt.Errorf("failed to stop executor: %w", err))
		}

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
