package execute

import (
	"context"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/source"
	"fmt"
	"log/slog"
)

type (
	UseCase struct {
		log              *slog.Logger
		unitOfWork       unitOfWork
		executionStorage executionStorage
	}

	unitOfWork interface {
		Do(context.Context, func(ctx context.Context) error) error
	}

	executionStorage interface {
		CreateExecution(context.Context, execution.Definition) error
	}
)

func NewUseCase(
	log *slog.Logger,
	unitOfWork unitOfWork,
	executionStorage executionStorage,
) *UseCase {
	return &UseCase{
		log:              log,
		unitOfWork:       unitOfWork,
		executionStorage: executionStorage,
	}
}

func (uc *UseCase) Execute(ctx context.Context, command Command) (result Result, err error) {
	srcs := make(map[source.DefinitionName]any, len(command.Sources))
	for _, src := range command.Sources {
		if _, exists := srcs[src.GetName()]; exists {
			err = fmt.Errorf("two or more sources have the same name `%s`", src.GetName())
			return
		}
		srcs[src.GetName()] = src
	}

	stages := make(map[execution.StageName]any, len(command.Stages))
	for _, stage := range command.Stages {
		if _, exists := stages[stage.Name]; exists {
			err = fmt.Errorf("two or more stages have the same name '%s'", stage.Name)
			return
		}
		stages[stage.Name] = struct{}{}

		jbs := make(map[job.DefinitionName]any, len(stage.Jobs))
		for _, jb := range stage.Jobs {
			jobName := jb.GetName()
			if _, exists := jbs[jobName]; exists {
				err = fmt.Errorf("two or more jobs in stage '%s' have the same name '%s'", stage.Name, jobName)
				return
			}
			jbs[jobName] = struct{}{}
		}
	}

	err = uc.unitOfWork.Do(ctx, func(ctx context.Context) error {
		e := execution.NewExecutionDefinition(command.Stages, command.Sources)
		if err = uc.executionStorage.CreateExecution(ctx, e); err != nil {
			return fmt.Errorf("failed to create execution in storage: %w", err)
		}

		result = Result{ExecutionID: e.ID}
		return nil
	})

	uc.log.Info("created execution", slog.String("execution", result.ExecutionID.String()))

	return
}
