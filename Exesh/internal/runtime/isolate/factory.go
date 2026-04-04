package isolate

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/runtime"
	"fmt"
	"sync"
)

const (
	defaultBoxIDStart = 0
	defaultBoxIDCount = 1000
)

type RuntimeFactory struct {
	mu         sync.Mutex
	boxIDStart int
	boxIDCount int
	nextBox    int
	inUse      map[int]bool
	jobTypes   map[job.Type]struct{}
}

func NewRuntimeFactory(jobTypes ...job.Type) *RuntimeFactory {
	jobTypesSet := make(map[job.Type]struct{}, len(jobTypes))
	for _, jobType := range jobTypes {
		jobTypesSet[jobType] = struct{}{}
	}

	return &RuntimeFactory{
		boxIDStart: defaultBoxIDStart,
		boxIDCount: defaultBoxIDCount,
		inUse:      make(map[int]bool),
		jobTypes:   jobTypesSet,
	}
}

func (f *RuntimeFactory) SupportsType(jobType job.Type) bool {
	_, ok := f.jobTypes[jobType]
	return ok
}

func (f *RuntimeFactory) Create(ctx context.Context) (runtime.Runtime, error) {
	boxID, err := f.acquireBoxID()
	if err != nil {
		return nil, err
	}

	rt := NewWithBoxIDAndOnStop(boxID, func() {
		f.releaseBoxID(boxID)
	})
	if err := rt.Init(ctx); err != nil {
		f.releaseBoxID(boxID)
		return nil, err
	}

	return rt, nil
}

func (f *RuntimeFactory) acquireBoxID() (int, error) {
	f.mu.Lock()
	defer f.mu.Unlock()

	if len(f.inUse) >= f.boxIDCount {
		return 0, fmt.Errorf("no available isolate boxes")
	}

	for i := 0; i < f.boxIDCount; i++ {
		idx := (f.nextBox + i) % f.boxIDCount
		boxID := f.boxIDStart + idx
		if f.inUse[boxID] {
			continue
		}

		f.inUse[boxID] = true
		f.nextBox = (idx + 1) % f.boxIDCount
		return boxID, nil
	}

	return 0, fmt.Errorf("no available isolate boxes")
}

func (f *RuntimeFactory) releaseBoxID(boxID int) {
	f.mu.Lock()
	delete(f.inUse, boxID)
	f.mu.Unlock()
}
