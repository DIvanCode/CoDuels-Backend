package heartbeat

import (
	"context"
	"exesh/internal/domain/execution"
	"log/slog"
)

type (
	Command struct {
		WorkerID  string
		DoneJobs  []execution.Result
		FreeSlots int
	}

	UseCase struct {
		log *slog.Logger

		workerPool       workerPool
		jobScheduler     jobScheduler
		artifactRegistry artifactRegistry
	}

	workerPool interface {
		Heartbeat(context.Context, string)
	}

	jobScheduler interface {
		PickJob(context.Context, string) *execution.Job
		DoneJob(context.Context, string, execution.Result)
	}

	artifactRegistry interface {
		PutArtifact(string, execution.JobID)
	}
)

func NewUseCase(log *slog.Logger, workerPool workerPool, jobScheduler jobScheduler, artifactRegistry artifactRegistry) *UseCase {
	return &UseCase{
		log: log,

		workerPool:       workerPool,
		jobScheduler:     jobScheduler,
		artifactRegistry: artifactRegistry,
	}
}

func (uc *UseCase) Heartbeat(ctx context.Context, command Command) ([]execution.Job, error) {
	uc.workerPool.Heartbeat(ctx, command.WorkerID)

	for _, jobResult := range command.DoneJobs {
		uc.artifactRegistry.PutArtifact(command.WorkerID, jobResult.GetJobID())
		uc.jobScheduler.DoneJob(ctx, command.WorkerID, jobResult)
	}

	jobs := make([]execution.Job, 0)
	for range command.FreeSlots {
		jobSpec := uc.jobScheduler.PickJob(ctx, command.WorkerID)
		if jobSpec == nil {
			break
		}
		jobs = append(jobs, *jobSpec)
	}

	return jobs, nil
}
