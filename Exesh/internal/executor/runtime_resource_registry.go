package executor

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/source"
	"fmt"
	"sync"
)

type RuntimeResourceRegistry struct {
	mu    sync.Mutex
	paths map[source.ID]string
}

func NewRuntimeResourceRegistry(capacity int) *RuntimeResourceRegistry {
	return &RuntimeResourceRegistry{
		paths: make(map[source.ID]string, capacity),
	}
}

func (r *RuntimeResourceRegistry) Set(sourceID source.ID, runtimePath string) {
	r.mu.Lock()
	defer r.mu.Unlock()
	r.paths[sourceID] = runtimePath
}

func (r *RuntimeResourceRegistry) Get(sourceID source.ID) (string, error) {
	r.mu.Lock()
	defer r.mu.Unlock()
	runtimePath, ok := r.paths[sourceID]
	if !ok {
		return "", fmt.Errorf("runtime path for source %s not found", sourceID.String())
	}
	return runtimePath, nil
}

func RegisterJobOutputRuntimePath(registry *RuntimeResourceRegistry, jobID job.ID, runtimePath string) {
	if registry == nil || runtimePath == "" {
		return
	}

	var sourceID source.ID
	if err := sourceID.FromString(jobID.String()); err != nil {
		return
	}
	registry.Set(sourceID, runtimePath)
}

func GetJobOutputRuntimePath(registry *RuntimeResourceRegistry, jobID job.ID) (string, error) {
	if registry == nil {
		return "", fmt.Errorf("runtime registry is nil")
	}

	var sourceID source.ID
	if err := sourceID.FromString(jobID.String()); err != nil {
		return "", fmt.Errorf("runtime source %s not found", jobID.String())
	}
	return registry.Get(sourceID)
}
