package heartbeat

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
	"log/slog"
	"time"
)

type (
	Command struct {
		WorkerID        string
		DoneJobs        []results.Result
		FreeSlots       int
		AvailableMemory int
	}

	UseCase struct {
		log *slog.Logger

		workerPool   workerPool
		jobScheduler jobScheduler
	}

	workerPool interface {
		Heartbeat(string, int, int)
		PutArtifact(string, job.ID, time.Time)
	}

	jobScheduler interface {
		PickJobs(context.Context, string, int, int) ([]jobs.Job, []sources.Source)
		DoneJob(context.Context, string, results.Result)
	}
)

func NewUseCase(log *slog.Logger, workerPool workerPool, jobScheduler jobScheduler) *UseCase {
	return &UseCase{
		log: log,

		workerPool:   workerPool,
		jobScheduler: jobScheduler,
	}
}

func (uc *UseCase) Heartbeat(ctx context.Context, command Command) ([]jobs.Job, []sources.Source) {
	if len(command.DoneJobs) > 0 {
		uc.log.Info("heartbeat with completed jobs",
			slog.String("worker", command.WorkerID),
			slog.Int("done_jobs", len(command.DoneJobs)),
			slog.Int("free_slots", command.FreeSlots),
			slog.Int("available_memory_mb", command.AvailableMemory),
		)
	}

	uc.workerPool.Heartbeat(command.WorkerID, command.FreeSlots, command.AvailableMemory)

	for _, jobResult := range command.DoneJobs {
		if jobResult.GetType() == result.Chain {
			for _, res := range jobResult.AsChain().Results {
				if res.GetHasOutput() {
					uc.workerPool.PutArtifact(command.WorkerID, res.GetJobID(), *res.GetArtifactTrashTime())
				}
			}
		} else {
			if jobResult.GetHasOutput() {
				uc.workerPool.PutArtifact(command.WorkerID, jobResult.GetJobID(), *jobResult.GetArtifactTrashTime())
			}
		}

		uc.jobScheduler.DoneJob(ctx, command.WorkerID, jobResult)
	}

	return uc.jobScheduler.PickJobs(ctx, command.WorkerID, command.FreeSlots, command.AvailableMemory)
}
