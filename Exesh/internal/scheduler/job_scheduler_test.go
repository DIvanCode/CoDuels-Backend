package scheduler

import (
	"context"
	"errors"
	"exesh/internal/config"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
	"io"
	"log/slog"
	"sync"
	"sync/atomic"
	"testing"
	"time"
)

func schedulerFixture() (*WorkerPool, *ExecutionScheduler, *JobScheduler) {
	log := slog.New(slog.NewTextHandler(io.Discard, nil))
	pool := NewWorkerPool(log, config.WorkerPoolConfig{})
	pool.Heartbeat("worker", 2, 128)
	executions := NewExecutionScheduler(log, config.ExecutionSchedulerConfig{}, nil, nil, nil, nil, pool, nil, nil)
	return pool, executions, NewJobScheduler(log, config.JobSchedulerConfig{PromisedJobsLimit: 5}, pool, executions)
}

func enqueueTestJob(s *ExecutionScheduler, id byte, memory int) *Job {
	jb := &Job{
		Job: jobs.Job{IJob: &jobs.RunCppJob{Details: job.Details{
			ID: job.ID{id}, Type: job.RunCpp, ExpectedTime: 1000, ExpectedMemory: memory,
		}}},
		Sources: func(context.Context) ([]sources.Source, error) { return nil, nil },
		OnDone:  func(context.Context, results.Result) {},
	}
	def := execution.NewExecutionDefinition(nil, nil, 1)
	def.SetScheduled(time.Now())
	ex := NewExecution(execution.NewExecution(def))
	ex.TotalExpectedTime = 1000
	ex.EnqueueJob(jb)
	jb.OnStart = func(context.Context) { ex.DequeueJob(jb) }
	s.executions[ex.ID] = ex
	return jb
}

func TestJobCompletionReleasesResourcesBeforeCallbackAndIgnoresRedelivery(t *testing.T) {
	ctx := context.Background()
	pool, executions, scheduler := schedulerFixture()
	jb := enqueueTestJob(executions, 1, 96)
	var callbacks atomic.Int32
	jb.OnDone = func(context.Context, results.Result) {
		// Re-enter the scheduler to verify the callback runs outside its mutex.
		scheduler.PickJobs(ctx, "worker", 0, 128)
		state := pool.getWorkersState()["worker"]
		if len(state.RunningJobs) != 0 || state.RunningJobsTotalExpectedMemory != 0 {
			t.Errorf("resources still allocated during completion: %+v", state)
		}
		callbacks.Add(1)
	}
	picked, _ := scheduler.PickJobs(ctx, "worker", 1, 128)
	if len(picked) != 1 || picked[0].GetID() != jb.GetID() {
		t.Fatalf("expected queued job, got %v", picked)
	}
	state := pool.getWorkersState()["worker"]
	if len(state.RunningJobs) != 1 || state.RunningJobsTotalExpectedMemory != 96 {
		t.Fatalf("incorrect running allocation: %+v", state)
	}
	res := results.NewRunResultOK(jb.GetID(), false, 12, 16)
	var deliveries sync.WaitGroup
	for range 8 {
		deliveries.Add(1)
		go func() {
			defer deliveries.Done()
			scheduler.DoneJob(ctx, "worker", res)
		}()
	}
	deliveries.Wait()
	if callbacks.Load() != 1 {
		t.Fatalf("completion callback called %d times", callbacks.Load())
	}
	// Unknown results are ignored even if there is no corresponding worker.
	scheduler.DoneJob(ctx, "unknown", results.NewRunResultOK(job.ID{99}, false, 0, 0))
}

func TestPromisedJobStartsAfterMemoryIsReleased(t *testing.T) {
	ctx := context.Background()
	pool, executions, scheduler := schedulerFixture()
	first := enqueueTestJob(executions, 1, 96)
	if picked, _ := scheduler.PickJobs(ctx, "worker", 1, 128); len(picked) != 1 {
		t.Fatal("first job did not start")
	}
	second := enqueueTestJob(executions, 2, 64)
	if picked, _ := scheduler.PickJobs(ctx, "worker", 1, 32); len(picked) != 0 {
		t.Fatal("job started without enough predicted memory")
	}
	if len(scheduler.promisedJobs) != 1 || scheduler.promisedJobs[0].GetID() != second.GetID() {
		t.Fatal("waiting job was not promised")
	}
	scheduler.DoneJob(ctx, "worker", results.NewRunResultOK(first.GetID(), false, 12, 16))
	picked, _ := scheduler.PickJobs(ctx, "worker", 1, 128)
	if len(picked) != 1 || picked[0].GetID() != second.GetID() || len(scheduler.promisedJobs) != 0 {
		t.Fatal("promised job did not start after completion")
	}
	state := pool.getWorkersState()["worker"]
	if len(state.RunningJobs) != 1 || state.RunningJobsTotalExpectedMemory != 64 {
		t.Fatalf("incorrect promised allocation: %+v", state)
	}
}

func TestSourceFailureReleasesResourcesAndCompletesOnce(t *testing.T) {
	ctx := context.Background()
	pool, executions, scheduler := schedulerFixture()
	jb := enqueueTestJob(executions, 1, 96)
	jb.Sources = func(context.Context) ([]sources.Source, error) { return nil, errors.New("artifact unavailable") }
	callbacks := 0
	jb.OnDone = func(_ context.Context, res results.Result) {
		callbacks++
		if res.GetError() == nil || res.GetError().Error() != "artifact unavailable" {
			t.Errorf("source error was lost: %v", res.GetError())
		}
	}
	if picked, _ := scheduler.PickJobs(ctx, "worker", 1, 128); len(picked) != 0 {
		t.Fatal("job with unavailable source was dispatched")
	}
	scheduler.DoneJob(ctx, "worker", results.NewRunResultOK(jb.GetID(), false, 0, 0))
	state := pool.getWorkersState()["worker"]
	if callbacks != 1 || len(state.RunningJobs) != 0 || state.RunningJobsTotalExpectedMemory != 0 {
		t.Fatalf("source failure leaked allocation or callback: callbacks=%d state=%+v", callbacks, state)
	}
}

func TestWorkerPoolReplacementAndRepeatedRemovalPreserveAccounting(t *testing.T) {
	pool, _, _ := schedulerFixture()
	pool.placeJob("worker", job.ID{1}, runningJob{expectedMemory: 64})
	pool.placeJob("worker", job.ID{2}, runningJob{expectedMemory: 16})
	pool.placeJob("worker", job.ID{1}, runningJob{expectedMemory: 32})
	state := pool.getWorkersState()["worker"]
	if len(state.RunningJobs) != 2 || state.RunningJobsTotalExpectedMemory != 48 {
		t.Fatalf("replaced allocation counted twice: %+v", state)
	}
	pool.removeJob("worker", job.ID{1})
	pool.removeJob("worker", job.ID{1})
	state = pool.getWorkersState()["worker"]
	if len(state.RunningJobs) != 1 || state.RunningJobsTotalExpectedMemory != 16 {
		t.Fatalf("repeated removal changed other allocations: %+v", state)
	}
}
