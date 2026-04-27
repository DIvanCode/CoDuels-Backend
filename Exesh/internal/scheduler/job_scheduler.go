package scheduler

import (
	"context"
	"exesh/internal/config"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
	"sort"
	"time"

	//"exesh/internal/lib/queue"
	"log/slog"
	"sync"
)

type (
	JobScheduler struct {
		log *slog.Logger
		cfg config.JobSchedulerConfig

		workerPool         *WorkerPool
		executionScheduler *ExecutionScheduler

		mu           sync.Mutex
		promisedJobs []promisedJob
		startedJobs  map[job.ID]*Job
	}

	promisedJob struct {
		*Job
		PromisedWorkerID string
		PromisedStartAt  time.Time
	}

	workerState struct {
		Slots                          int
		Memory                         int
		RunningJobs                    []runningJob
		RunningJobsTotalExpectedMemory int
	}

	scanlineEvent struct {
		EventTime time.Time
		Type      int
		Memory    int
	}
)

func NewJobScheduler(
	log *slog.Logger,
	cfg config.JobSchedulerConfig,
	workerPool *WorkerPool,
	executionScheduler *ExecutionScheduler) *JobScheduler {
	return &JobScheduler{
		log: log,
		cfg: cfg,

		workerPool:         workerPool,
		executionScheduler: executionScheduler,

		mu:           sync.Mutex{},
		promisedJobs: make([]promisedJob, 0),
		startedJobs:  make(map[job.ID]*Job),
	}
}

func (s *JobScheduler) PickJobs(ctx context.Context, workerID string, slots, memory int) ([]jobs.Job, []sources.Source) {
	pickedJobs := make([]jobs.Job, 0)
	pickedSources := make([]sources.Source, 0)
	for range slots {
		jb, srcs := s.pickJob(ctx, workerID, memory)
		if jb == nil {
			break
		}

		pickedJobs = append(pickedJobs, *jb)
		pickedSources = append(pickedSources, srcs...)
		memory -= jb.GetExpectedMemory()
	}

	return pickedJobs, pickedSources
}

func (s *JobScheduler) DoneJob(ctx context.Context, workerID string, res results.Result) {
	prepareCallback := func() doneCallback {
		s.mu.Lock()
		defer s.mu.Unlock()

		jobID := res.GetJobID()
		jb, ok := s.startedJobs[jobID]
		if !ok {
			return nil
		}

		delete(s.startedJobs, jobID)
		s.workerPool.removeJob(workerID, jobID)

		return jb.OnDone
	}

	cb := prepareCallback()
	if cb != nil {
		cb(ctx, res)
	}
}

func (s *JobScheduler) pickJob(ctx context.Context, workerID string, memory int) (*jobs.Job, []sources.Source) {
	s.mu.Lock()

	workers := s.workerPool.getWorkersState()

	promisedJobs := make([]promisedJob, 0, len(s.promisedJobs))
	copy(promisedJobs, s.promisedJobs)
	s.promisedJobs = make([]promisedJob, 0)

	var pickedJob *Job = nil
	now := time.Now()
	for _, jb := range promisedJobs {
		if pickedJob == nil && s.canStartNowOnWorker(workerID, jb.Job, now, workers, s.promisedJobs) {
			pickedJob = jb.Job
			s.startedJobs[pickedJob.GetID()] = pickedJob
			s.workerPool.placeJob(workerID, jb.GetID(), runningJob{
				expectedTime:   jb.GetExpectedTime(),
				expectedMemory: jb.GetExpectedMemory(),
				startedAt:      now,
			})
			workers = s.workerPool.getWorkersState()
			continue
		}

		jb.PromisedWorkerID, jb.PromisedStartAt = s.getBestPromise(jb.Job, now, workers, s.promisedJobs)
		s.promisedJobs = append(s.promisedJobs, jb)
	}

	if pickedJob == nil {
		jbs := s.executionScheduler.pickJobs()
		for _, jb := range jbs {
			if s.canStartNowOnWorker(workerID, jb, now, workers, s.promisedJobs) {
				pickedJob = jb
				s.startedJobs[pickedJob.GetID()] = pickedJob
				s.workerPool.placeJob(workerID, jb.GetID(), runningJob{
					expectedTime:   jb.GetExpectedTime(),
					expectedMemory: jb.GetExpectedMemory(),
					startedAt:      now,
				})
				break
			}

			if len(s.promisedJobs) < s.cfg.PromisedJobsLimit {
				promisedWorkerID, promisedStartAt := s.getBestPromise(jb, now, workers, s.promisedJobs)
				s.promisedJobs = append(s.promisedJobs, promisedJob{
					Job:              jb,
					PromisedWorkerID: promisedWorkerID,
					PromisedStartAt:  promisedStartAt,
				})
			}
		}
	}

	s.mu.Unlock()

	if pickedJob == nil {
		return nil, nil
	}

	pickedJob.OnStart(ctx)

	srcs, err := pickedJob.Sources(ctx)
	if err != nil {
		res := results.Error(pickedJob.Job, err)
		s.DoneJob(ctx, workerID, res)
		return s.pickJob(ctx, workerID, memory)
	}

	return &pickedJob.Job, srcs
}

func (s *JobScheduler) canStartNowOnWorker(
	workerID string,
	jb *Job,
	now time.Time,
	workers map[string]workerState,
	promisedJobsState []promisedJob,
) bool {
	w := workers[workerID]
	if len(w.RunningJobs)+1 > w.Slots || w.RunningJobsTotalExpectedMemory+jb.GetExpectedMemory() > w.Memory {
		return false
	}

	newWorkerState := cloneWorkerState(w)
	newWorkerState.RunningJobs = append(w.RunningJobs, runningJob{
		expectedTime:   jb.GetExpectedTime(),
		expectedMemory: jb.GetExpectedMemory(),
		startedAt:      now,
	})
	workers[workerID] = newWorkerState
	defer func() {
		workers[workerID] = w
	}()

	newPromisedJobsState := make([]promisedJob, 0, len(promisedJobsState))
	for _, promisedJb := range s.promisedJobs {
		promisedWorkerID, promisedStartAt := s.getBestPromise(promisedJb.Job, now, workers, newPromisedJobsState)
		if promisedStartAt.After(promisedJb.PromisedStartAt) {
			return false
		}
		newPromisedJobsState = append(newPromisedJobsState, promisedJob{
			Job:              promisedJb.Job,
			PromisedWorkerID: promisedWorkerID,
			PromisedStartAt:  promisedStartAt,
		})
	}

	return true
}

func (s *JobScheduler) getBestPromise(
	jb *Job,
	now time.Time,
	workers map[string]workerState,
	promisedJobs []promisedJob,
) (string, time.Time) {
	expectedJobTimeDuration := time.Millisecond * time.Duration(jb.GetExpectedTime())

	workerPromisedJobs := make(map[string][]promisedJob)
	for _, promisedJb := range promisedJobs {
		workerID := promisedJb.PromisedWorkerID
		if _, ok := workers[workerID]; !ok {
			workerPromisedJobs[workerID] = make([]promisedJob, 0)
		}
		workerPromisedJobs[workerID] = append(workerPromisedJobs[workerID], promisedJb)
	}

	bestWorker := ""
	var bestStartedAt *time.Time = nil
	for id, w := range workers {
		events := make([]scanlineEvent, 0)

		for _, runningJb := range w.RunningJobs {
			expectedTimeDuration := time.Millisecond * time.Duration(runningJb.expectedTime)
			expectedFinishAt := runningJb.startedAt.Add(expectedTimeDuration)
			if expectedFinishAt.Before(now) {
				continue
			}

			events = append(events, scanlineEvent{
				EventTime: now,
				Type:      1,
				Memory:    runningJb.expectedMemory,
			})
			events = append(events, scanlineEvent{
				EventTime: expectedFinishAt,
				Type:      -1,
				Memory:    runningJb.expectedMemory,
			})
		}

		for _, promisedJb := range workerPromisedJobs[id] {
			expectedTimeDuration := time.Millisecond * time.Duration(promisedJb.GetExpectedTime())
			expectedFinishAt := promisedJb.PromisedStartAt.Add(expectedTimeDuration)

			events = append(events, scanlineEvent{
				EventTime: promisedJb.PromisedStartAt,
				Type:      1,
				Memory:    promisedJb.GetExpectedMemory(),
			})
			events = append(events, scanlineEvent{
				EventTime: expectedFinishAt,
				Type:      -1,
				Memory:    promisedJb.GetExpectedMemory(),
			})
		}

		sort.Slice(events, func(i, j int) bool {
			if events[i].EventTime != events[j].EventTime {
				return events[i].EventTime.Before(events[j].EventTime)
			}
			if events[i].Type != events[j].Type {
				return events[i].Type > events[j].Type
			}
			return events[i].Memory < events[j].Memory
		})

		ptr := 0
		usedSlots := 0
		expectedUsedMemory := 0
		startedAt := &now
		for ptr < len(events) {
			eventTime := events[ptr].EventTime
			if startedAt != nil && startedAt.Add(expectedJobTimeDuration).Before(eventTime) {
				break
			}

			for ptr < len(events) && events[ptr].EventTime.Equal(eventTime) {
				usedSlots += events[ptr].Type
				expectedUsedMemory += events[ptr].Type * events[ptr].Memory
				ptr++
			}

			if usedSlots+1 > w.Slots || expectedUsedMemory+jb.GetExpectedMemory() > w.Memory {
				startedAt = nil
			}

			if startedAt == nil && usedSlots+1 <= w.Slots && expectedUsedMemory+jb.GetExpectedMemory() <= w.Memory {
				startedAt = &eventTime
			}
		}

		if startedAt == nil {
			continue
		}

		if bestStartedAt == nil || startedAt.Before(*bestStartedAt) {
			bestWorker = id
			bestStartedAt = startedAt
		}
	}

	if bestStartedAt == nil {
		jobID := jb.GetID()
		s.log.Error("cannot find worker to promise job",
			slog.String("job_id", jobID.String()),
			slog.Int("expected_memory", jb.GetExpectedMemory()))
		return "", now.Add(time.Minute)
	}

	return bestWorker, *bestStartedAt
}

func cloneWorkerState(w workerState) workerState {
	state := workerState{
		Slots:                          w.Slots,
		Memory:                         w.Memory,
		RunningJobs:                    make([]runningJob, 0, len(w.RunningJobs)),
		RunningJobsTotalExpectedMemory: w.RunningJobsTotalExpectedMemory,
	}
	for _, jb := range w.RunningJobs {
		state.RunningJobs = append(state.RunningJobs, jb)
	}
	return state
}
