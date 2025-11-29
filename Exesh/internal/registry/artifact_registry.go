package registry

import (
	"exesh/internal/config"
	"exesh/internal/domain/execution"
	"fmt"
	"log/slog"
	"math/rand/v2"
	"sync"
	"time"
)

type (
	ArtifactRegistry struct {
		log *slog.Logger
		cfg config.ArtifactRegistryConfig

		workerPool workerPool

		mu              sync.Mutex
		workerArtifacts map[string]map[execution.JobID]time.Time
	}

	workerPool interface {
		IsAlive(string) bool
	}
)

func NewArtifactRegistry(log *slog.Logger, cfg config.ArtifactRegistryConfig, workerPool workerPool) *ArtifactRegistry {
	return &ArtifactRegistry{
		log: log,
		cfg: cfg,

		workerPool: workerPool,

		workerArtifacts: make(map[string]map[execution.JobID]time.Time),
	}
}

func (r *ArtifactRegistry) GetWorker(jobID execution.JobID) (workerID string, err error) {
	r.mu.Lock()
	defer r.mu.Unlock()

	workers := make([]string, 0)
	for worker, artifacts := range r.workerArtifacts {
		if trashTime, ok := artifacts[jobID]; ok && r.workerPool.IsAlive(worker) {
			if time.Now().After(trashTime) {
				delete(r.workerArtifacts[worker], jobID)
			} else {
				workers = append(workers, worker)
			}
		}
	}

	if len(workers) == 0 {
		err = fmt.Errorf("worker for artifact not found")
		return
	}

	workerID = workers[rand.N(len(workers))]
	return
}

func (r *ArtifactRegistry) PutArtifact(workerID string, jobID execution.JobID) {
	r.mu.Lock()
	defer r.mu.Unlock()

	artifacts := r.workerArtifacts[workerID]
	if artifacts == nil {
		artifacts = make(map[execution.JobID]time.Time)
	}
	trashTime := time.Now().Add(r.cfg.ArtifactTTL)
	artifacts[jobID] = trashTime
	r.workerArtifacts[workerID] = artifacts
}
