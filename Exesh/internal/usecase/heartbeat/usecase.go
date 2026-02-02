package heartbeat

import (
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
	"log/slog"
)

type (
	Command struct {
		WorkerID  string
		DoneJobs  []results.Result
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
		PickJob(context.Context, string) (*jobs.Job, []sources.Source)
		DoneJob(context.Context, string, results.Result)
	}

	artifactRegistry interface {
		PutArtifact(string, job.ID)
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

func (uc *UseCase) Heartbeat(ctx context.Context, command Command) ([]jobs.Job, []sources.Source, error) {
	uc.workerPool.Heartbeat(ctx, command.WorkerID)

	for _, jobResult := range command.DoneJobs {
		uc.artifactRegistry.PutArtifact(command.WorkerID, jobResult.GetJobID())
		uc.jobScheduler.DoneJob(ctx, command.WorkerID, jobResult)
	}

	jbs := make([]jobs.Job, 0)
	srcs := make([]sources.Source, 0)
	for range command.FreeSlots {
		jb, src := uc.jobScheduler.PickJob(ctx, command.WorkerID)
		if jb == nil {
			break
		}
		jbs = append(jbs, *jb)
		srcs = append(srcs, src...)
	}

	return jbs, srcs, nil
}
