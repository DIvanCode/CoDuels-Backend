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

		mu:         sync.Mutex{},
		heartbeats: make(map[string]chan any),
		stop:       false,
	}
}

func (p *WorkerPool) Heartbeat(ctx context.Context, workerID string) {
	if !p.IsAlive(workerID) {
		p.createWorker(workerID)
	}

	p.mu.Lock()
	heartbeat, ok := p.heartbeats[workerID]
	if p.stop || !ok {
		return
	}
	p.mu.Unlock()

	heartbeat <- struct{}{}
}

func (p *WorkerPool) IsAlive(workerID string) bool {
	p.mu.Lock()
	defer p.mu.Unlock()

	_, ok := p.heartbeats[workerID]
	return ok
}

func (p *WorkerPool) StopObservers() {
	p.mu.Lock()
	defer p.mu.Unlock()

	p.stop = true
}

func (p *WorkerPool) createWorker(workerID string) {
	p.mu.Lock()
	defer p.mu.Unlock()

	p.heartbeats[workerID] = make(chan any)
	go p.runObserver(workerID)
}

func (p *WorkerPool) runObserver(workerID string) {
	for {
		p.mu.Lock()
		stop := p.stop
		heartbeat, ok := p.heartbeats[workerID]
		p.mu.Unlock()

		if stop || !ok {
			break
		}

		timer := time.NewTicker(p.cfg.WorkerDieAfter)

		select {
		case <-timer.C:
			p.deleteWorker(workerID)
		case <-heartbeat:
			break
		}
	}
}

func (p *WorkerPool) deleteWorker(workerID string) {
	p.mu.Lock()
	defer p.mu.Unlock()

	delete(p.heartbeats, workerID)
}
