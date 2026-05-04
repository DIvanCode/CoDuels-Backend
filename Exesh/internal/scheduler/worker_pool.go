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
		events  EventRecorder
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
		memoryOffset   int
		startedAt      time.Time
	}
)

func NewWorkerPool(log *slog.Logger, cfg config.WorkerPoolConfig, events EventRecorder) *WorkerPool {
	if events == nil {
		events = NoopEventRecorder{}
	}
	return &WorkerPool{
		log: log,
		cfg: cfg,

		mu:      sync.Mutex{},
		workers: make(map[string]*worker),
		events:  events,
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
					p.events.RecordWorkerEvent(ctx, WorkerEvent{
						Type:     "removed",
						WorkerID: w,
						At:       time.Now(),
					})
				}
				p.mu.Unlock()
			}
		}
	}()
}

func (p *WorkerPool) Heartbeat(workerID string, slots, memory int) {
	p.mu.Lock()
	defer p.mu.Unlock()

	now := time.Now()
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
			LastHeartbeat:                  now,
			Artifacts:                      make(map[job.ID]time.Time),
			RunningJobs:                    make(map[job.ID]runningJob),
			RunningJobsTotalExpectedMemory: 0,
		}
		p.events.RecordWorkerEvent(context.Background(), WorkerEvent{
			Type:              "registered",
			WorkerID:          workerID,
			TotalSlots:        slots,
			TotalMemoryMB:     memory,
			FreeSlots:         slots,
			AvailableMemoryMB: memory,
			At:                now,
		})
	}

	p.workers[workerID].LastHeartbeat = now
	w := p.workers[workerID]
	p.events.RecordWorkerEvent(context.Background(), WorkerEvent{
		Type:              "heartbeat",
		WorkerID:          workerID,
		TotalSlots:        w.Slots,
		TotalMemoryMB:     w.Memory,
		FreeSlots:         w.Slots - len(w.RunningJobs),
		AvailableMemoryMB: w.Memory - w.RunningJobsTotalExpectedMemory,
		RunningJobs:       len(w.RunningJobs),
		UsedMemoryMB:      w.RunningJobsTotalExpectedMemory,
		At:                now,
	})
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

func (p *WorkerPool) placeJob(workerID string, jobID job.ID, jb runningJob) int {
	p.mu.Lock()
	defer p.mu.Unlock()

	w := p.workers[workerID]
	if exJb, ok := w.RunningJobs[jobID]; ok {
		w.RunningJobsTotalExpectedMemory -= exJb.expectedMemory
	}
	jb.memoryOffset = firstFreeMemoryOffset(w.RunningJobs, jb.expectedMemory, w.Memory)
	w.RunningJobs[jobID] = jb
	w.RunningJobsTotalExpectedMemory += jb.expectedMemory
	p.events.RecordWorkerEvent(context.Background(), WorkerEvent{
		Type:              "job_placed",
		WorkerID:          workerID,
		TotalSlots:        w.Slots,
		TotalMemoryMB:     w.Memory,
		FreeSlots:         w.Slots - len(w.RunningJobs),
		AvailableMemoryMB: w.Memory - w.RunningJobsTotalExpectedMemory,
		RunningJobs:       len(w.RunningJobs),
		UsedMemoryMB:      w.RunningJobsTotalExpectedMemory,
		At:                time.Now(),
	})
	return jb.memoryOffset
}

func (p *WorkerPool) removeJob(workerID string, jobID job.ID) {
	p.mu.Lock()
	defer p.mu.Unlock()

	w := p.workers[workerID]
	if jb, ok := w.RunningJobs[jobID]; ok {
		w.RunningJobsTotalExpectedMemory -= jb.expectedMemory
		delete(w.RunningJobs, jobID)
		p.events.RecordWorkerEvent(context.Background(), WorkerEvent{
			Type:              "job_removed",
			WorkerID:          workerID,
			TotalSlots:        w.Slots,
			TotalMemoryMB:     w.Memory,
			FreeSlots:         w.Slots - len(w.RunningJobs),
			AvailableMemoryMB: w.Memory - w.RunningJobsTotalExpectedMemory,
			RunningJobs:       len(w.RunningJobs),
			UsedMemoryMB:      w.RunningJobsTotalExpectedMemory,
			At:                time.Now(),
		})
	}
}

func firstFreeMemoryOffset(runningJobs map[job.ID]runningJob, memory int, totalMemory int) int {
	type interval struct {
		from int
		to   int
	}

	intervals := make([]interval, 0, len(runningJobs))
	for _, jb := range runningJobs {
		intervals = append(intervals, interval{
			from: jb.memoryOffset,
			to:   jb.memoryOffset + jb.expectedMemory,
		})
	}

	for offset := 0; offset+memory <= totalMemory; offset++ {
		ok := true
		for _, in := range intervals {
			if offset < in.to && offset+memory > in.from {
				ok = false
				offset = in.to - 1
				break
			}
		}
		if ok {
			return offset
		}
	}

	return 0
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
