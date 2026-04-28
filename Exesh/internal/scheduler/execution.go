package scheduler

import (
	"exesh/internal/domain/execution"
	"exesh/internal/lib/queue"
	"math"
	"math/rand/v2"
	"sync"
	"time"
)

const (
	priorityTimeExpectedFactor = 0.2
	priorityFailedTryFactor    = 3.0
)

type Execution struct {
	*execution.Execution

	mu sync.Mutex

	scheduledJobs queue.Queue[*Job]

	TotalExpectedTime         int64
	TotalDoneJobsExpectedTime int64
}

func NewExecution(ex *execution.Execution) *Execution {
	totalExpectedTime := int64(0)
	for _, jb := range ex.JobByName {
		totalExpectedTime += int64(jb.GetExpectedTime())
	}

	return &Execution{
		Execution: ex,

		mu: sync.Mutex{},

		scheduledJobs: *queue.NewQueue[*Job](),

		TotalExpectedTime:         totalExpectedTime,
		TotalDoneJobsExpectedTime: 0,
	}
}

func (ex *Execution) EnqueueJob(jb *Job) {
	ex.mu.Lock()
	defer ex.mu.Unlock()

	ex.scheduledJobs.Enqueue(jb)
}

func (ex *Execution) GetPeekJob() *Job {
	ex.mu.Lock()
	defer ex.mu.Unlock()

	jb := ex.scheduledJobs.Peek()
	if jb == nil {
		return nil
	}
	return *jb
}

func (ex *Execution) DequeueJob(jb *Job) {
	ex.mu.Lock()
	defer ex.mu.Unlock()

	peekJob := ex.scheduledJobs.Peek()
	if peekJob == nil || (*peekJob).GetID() != jb.GetID() {
		return
	}

	ex.scheduledJobs.Dequeue()
}

func (ex *Execution) GetPriority(now time.Time) float64 {
	ex.mu.Lock()
	defer ex.mu.Unlock()
	return ex.executionProgressAndRetriesBasedPriority(now)
}

func (ex *Execution) scheduleTimeBasedPriority(now time.Time) float64 {
	return ex.getProgressTime(now)
}

func (ex *Execution) randomBasedPriority(now time.Time) float64 {
	_ = now
	return rand.Float64()
}

func (ex *Execution) executionProgressAndRetriesBasedPriority(now time.Time) float64 {
	progressTime := ex.getProgressTime(now)
	totalExpectedTime := float64(ex.TotalExpectedTime)
	totalDoneJobsExpectedTime := float64(ex.TotalDoneJobsExpectedTime)
	retryCount := max(0, ex.Tries-1)
	retriesPower := math.Pow(priorityFailedTryFactor, float64(retryCount))
	return retriesPower * progressTime / (priorityTimeExpectedFactor*totalExpectedTime + totalDoneJobsExpectedTime)
}

func (ex *Execution) getProgressTime(now time.Time) float64 {
	return float64(now.Sub(*ex.ScheduledAt).Milliseconds())
}
