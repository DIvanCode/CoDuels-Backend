package registry

import (
	"exesh/internal/domain/execution"
	"fmt"
	"log/slog"
)

type ArtifactRegistry struct {
	log *slog.Logger
}

func NewArtifactRegistry(log *slog.Logger) *ArtifactRegistry {
	return &ArtifactRegistry{
		log: log,
	}
}

func (r *ArtifactRegistry) GetWorker(jobID execution.JobID) (worker string, err error) {
	err = fmt.Errorf("not implemented")
	return
}
