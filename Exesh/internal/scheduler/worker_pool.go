package scheduler

import (
	"context"
	"exesh/internal/config"
	"exesh/internal/domain/execution/job"
	"fmt"
	"log/slog"
	"math/rand/v2"
	"sync"
	"time"
)

type (
	WorkerPool struct {
		log *slog.Logger
		cfg config.WorkerPoolConfig

		mu      sync.Mutex
		workers map[string]*worker
	}

	worker struct {
		ID                             string
		Slots                          int
		Memory                         int
		LastHeartbeat                  time.Time
		Artifacts                      map[job.ID]time.Time
		RunningJobs                    map[job.ID]runningJob
		RunningJobsTotalExpectedMemory int
	}

	runningJob struct {
		expectedTime   int
		expectedMemory int
		startedAt      time.Time
	}
)

func NewWorkerPool(log *slog.Logger, cfg config.WorkerPoolConfig) *WorkerPool {
	return &WorkerPool{
		log: log,
		cfg: cfg,

		mu:      sync.Mutex{},
		workers: make(map[string]*worker),
	}
}

func (p *WorkerPool) StartObserver(ctx context.Context) {
	go func() {
		timer := time.NewTicker(p.cfg.WorkerDieAfter)

		for {
			select {
			case <-ctx.Done():
				return
			case <-timer.C:
				p.mu.Lock()
				deadWorkers := make([]string, 0)
				for _, w := range p.workers {
					if w.LastHeartbeat.Add(p.cfg.WorkerDieAfter).Before(time.Now()) {
						deadWorkers = append(deadWorkers, w.ID)
					}
				}
				for _, w := range deadWorkers {
					p.log.Warn("worker removed after missed heartbeat", slog.String("worker", w))
					delete(p.workers, w)
				}
				p.mu.Unlock()
			}
		}
	}()
}

func (p *WorkerPool) Heartbeat(workerID string, slots, memory int) {
	p.mu.Lock()
	defer p.mu.Unlock()

	if _, ok := p.workers[workerID]; !ok {
		p.log.Info("worker registered",
			slog.String("worker", workerID),
			slog.Int("slots", slots),
			slog.Int("memory_mb", memory),
		)
		p.workers[workerID] = &worker{
			ID:                             workerID,
			Slots:                          slots,
			Memory:                         memory,
			LastHeartbeat:                  time.Now(),
			Artifacts:                      make(map[job.ID]time.Time),
			RunningJobs:                    make(map[job.ID]runningJob),
			RunningJobsTotalExpectedMemory: 0,
		}
	}

	p.workers[workerID].LastHeartbeat = time.Now()
}

func (p *WorkerPool) PutArtifact(workerID string, jobID job.ID, trashTime time.Time) {
	p.mu.Lock()
	defer p.mu.Unlock()

	p.workers[workerID].Artifacts[jobID] = trashTime
}

func (p *WorkerPool) getWorkersState() map[string]workerState {
	p.mu.Lock()
	defer p.mu.Unlock()

	workers := make(map[string]workerState)
	for _, w := range p.workers {
		state := workerState{
			Slots:                          w.Slots,
			Memory:                         w.Memory,
			RunningJobs:                    make([]runningJob, 0, len(w.RunningJobs)),
			RunningJobsTotalExpectedMemory: w.RunningJobsTotalExpectedMemory,
		}
		for _, jb := range w.RunningJobs {
			state.RunningJobs = append(state.RunningJobs, jb)
		}
		workers[w.ID] = state
	}

	return workers
}

func (p *WorkerPool) placeJob(workerID string, jobID job.ID, jb runningJob) {
	p.mu.Lock()
	defer p.mu.Unlock()

	w := p.workers[workerID]
	if exJb, ok := w.RunningJobs[jobID]; ok {
		w.RunningJobsTotalExpectedMemory -= exJb.expectedMemory
	}
	w.RunningJobs[jobID] = jb
	w.RunningJobsTotalExpectedMemory += jb.expectedMemory
}

func (p *WorkerPool) removeJob(workerID string, jobID job.ID) {
	p.mu.Lock()
	defer p.mu.Unlock()

	w := p.workers[workerID]
	if jb, ok := w.RunningJobs[jobID]; ok {
		w.RunningJobsTotalExpectedMemory -= jb.expectedMemory
		delete(w.RunningJobs, jobID)
	}
}

func (p *WorkerPool) getWorkerWithArtifact(jobID job.ID) (workerID string, err error) {
	p.mu.Lock()
	defer p.mu.Unlock()

	ws := make([]*worker, 0)
	for _, w := range p.workers {
		if trashTime, ok := w.Artifacts[jobID]; ok {
			if time.Now().Add(time.Minute).After(trashTime) {
				delete(w.Artifacts, jobID)
			} else {
				ws = append(ws, w)
			}
		}
	}

	if len(ws) == 0 {
		err = fmt.Errorf("worker for artifact not found")
		return
	}

	workerID = ws[rand.N(len(ws))].ID
	return
}
