package scheduler

import (
	"exesh/internal/domain/execution"
	"exesh/internal/lib/queue"
	"math/rand/v2"
	"sync"
	"time"
)

type Execution struct {
	*execution.Execution

	mu   sync.Mutex
	jobs queue.Queue[*Job]
}

func NewExecution(ex *execution.Execution) *Execution {
	return &Execution{
		Execution: ex,

		mu:   sync.Mutex{},
		jobs: *queue.NewQueue[*Job](),
	}
}

func (ex *Execution) EnqueueJob(jb *Job) {
	ex.mu.Lock()
	defer ex.mu.Unlock()

	ex.jobs.Enqueue(jb)
}

func (ex *Execution) GetPeekJob() *Job {
	ex.mu.Lock()
	defer ex.mu.Unlock()

	jb := ex.jobs.Peek()
	if jb == nil {
		return nil
	}
	return *jb
}

func (ex *Execution) DequeueJob(jb *Job) {
	ex.mu.Lock()
	defer ex.mu.Unlock()

	peekJob := ex.jobs.Peek()
	if peekJob == nil || (*peekJob).GetID() != jb.GetID() {
		return
	}

	ex.jobs.Dequeue()
}

func (ex *Execution) GetPriority(now time.Time) float64 {
	return ex.randomBasedPriority(now)
}

func (ex *Execution) scheduleTimeBasedPriority(now time.Time) float64 {
	return ex.getProgressTime(now)
}

func (ex *Execution) randomBasedPriority(now time.Time) float64 {
	_ = now
	return rand.Float64()
}

func (ex *Execution) getProgressTime(now time.Time) float64 {
	return float64(now.Sub(*ex.ScheduledAt).Milliseconds())
}
