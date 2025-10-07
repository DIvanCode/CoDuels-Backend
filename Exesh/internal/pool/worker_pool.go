package pool

import (
	"context"
	"exesh/internal/config"
	"log/slog"
	"sync"
	"time"
)

type WorkerPool struct {
	log *slog.Logger
	cfg config.WorkerPoolConfig

	heartbeats map[string]chan any

	mu   sync.Mutex
	stop bool
}

func NewWorkerPool(log *slog.Logger, cfg config.WorkerPoolConfig) *WorkerPool {
	return &WorkerPool{
		log: log,
		cfg: cfg,

		heartbeats: make(map[string]chan any),

		mu:   sync.Mutex{},
		stop: false,
	}
}

func (p *WorkerPool) Heartbeat(ctx context.Context, workerID string) {
	if _, ok := p.heartbeats[workerID]; !ok {
		p.createWorker(workerID)
	}

	p.mu.Lock()
	if p.stop {
		return
	}
	p.mu.Unlock()

	p.heartbeats[workerID] <- struct{}{}
}

func (p *WorkerPool) IsAlive(workerID string) bool {
	_, ok := p.heartbeats[workerID]
	return ok
}

func (p *WorkerPool) StopObservers() {
	p.mu.Lock()
	p.stop = true
	p.mu.Unlock()
}

func (p *WorkerPool) createWorker(workerID string) {
	p.heartbeats[workerID] = make(chan any)
	go p.runObserver(workerID)
}

func (p *WorkerPool) runObserver(workerID string) {
	for {
		p.mu.Lock()
		if p.stop {
			break
		}
		p.mu.Unlock()

		timer := time.NewTicker(p.cfg.WorkerDieAfter)

		select {
		case <-timer.C:
			p.deleteWorker(workerID)
		case <-p.heartbeats[workerID]:
			break
		}
	}
}

func (p *WorkerPool) deleteWorker(workerID string) {
	delete(p.heartbeats, workerID)
}
