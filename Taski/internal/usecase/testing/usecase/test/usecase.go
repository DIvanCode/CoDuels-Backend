package test

import (
	"context"
	"fmt"
	"log/slog"
	"taski/internal/domain/task"
	"taski/internal/domain/testing"
	"taski/internal/domain/testing/execution"
	"taski/internal/domain/testing/source/sources"
	"taski/internal/factory"
)

type (
	Command struct {
		ExternalSolutionID testing.ExternalSolutionID
		TaskID             task.ID
		Solution           string
		Lang               task.Language
	}

	UseCase struct {
		log                  *slog.Logger
		taskStorage          taskStorage
		unitOfWork           unitOfWork
		solutionStorage      solutionStorage
		executeClient        executeClient
		downloadTaskEndpoint string
	}

	taskStorage interface {
		Get(task.ID) (task task.Task, unlock func(), err error)
	}

	unitOfWork interface {
		Do(context.Context, func(ctx context.Context) error) error
	}

	solutionStorage interface {
		Create(context.Context, testing.Solution) error
	}

	executeClient interface {
		Execute(context.Context, execution.Stages, sources.Sources) (execution.ID, error)
	}
)

func NewUseCase(
	log *slog.Logger,
	storage taskStorage,
	unitOfWork unitOfWork,
	solutionStorage solutionStorage,
	executeClient executeClient,
	downloadTaskEndpoint string,
) *UseCase {
	return &UseCase{
		log:                  log,
		taskStorage:          storage,
		unitOfWork:           unitOfWork,
		solutionStorage:      solutionStorage,
		executeClient:        executeClient,
		downloadTaskEndpoint: downloadTaskEndpoint,
	}
}

func (uc *UseCase) Test(ctx context.Context, command Command) error {
	return uc.unitOfWork.Do(ctx, func(ctx context.Context) error {
		t, unlock, err := uc.taskStorage.Get(command.TaskID)
		if err != nil {
			uc.log.Error("failed to get task from storage", slog.Any("err", err))
			return fmt.Errorf("failed to get task from storage")
		}
		defer unlock()

		testingStrategy, err := factory.NewTestingStrategyFactory().CreateStrategy(t,
			command.Solution, command.Lang, uc.downloadTaskEndpoint)
		if err != nil {
			uc.log.Error("failed to create testing strategy", slog.Any("err", err))
			return fmt.Errorf("failed to create testing steps")
		}

		executionID, err := uc.executeClient.Execute(ctx, testingStrategy.GetStages(), testingStrategy.GetSources())
		if err != nil {
			uc.log.Error("failed to execute testing steps", slog.Any("err", err))
			return fmt.Errorf("failed to execute testing steps")
		}

		sol := testing.NewSolution(
			command.ExternalSolutionID,
			command.TaskID,
			command.Solution,
			command.Lang,
			testingStrategy,
			executionID)
		if err := uc.solutionStorage.Create(ctx, sol); err != nil {
			uc.log.Error("failed to save solution to storage",
				slog.String("external_id", string(sol.ExternalID)),
				slog.Any("err", err))
			return fmt.Errorf("failed to save solution to storage: %w", err)
		}

		return nil
	})
}
