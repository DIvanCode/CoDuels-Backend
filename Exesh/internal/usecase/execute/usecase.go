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
		calc             calculator
	}

	unitOfWork interface {
		Do(context.Context, func(ctx context.Context) error) error
	}

	executionStorage interface {
		CreateExecution(context.Context, execution.Definition) error
	}

	calculator interface {
		LoadCategoryStats(context.Context, execution.StageDefinitions) (execution.CategoryStats, error)
		CalculateWeight(execution.StageDefinitions, execution.CategoryStats) int64
	}
)

func NewUseCase(
	log *slog.Logger,
	unitOfWork unitOfWork,
	executionStorage executionStorage,
	calc calculator,
) *UseCase {
	return &UseCase{
		log:              log,
		unitOfWork:       unitOfWork,
		executionStorage: executionStorage,
		calc:             calc,
	}
}

func (uc *UseCase) Execute(ctx context.Context, command Command) (result Result, err error) {
	srcs := make(map[source.DefinitionName]any, len(command.Sources))
	for _, src := range command.Sources {
		if _, exists := srcs[src.GetName()]; exists {
			err = fmt.Errorf("two or more sources have the same name `%s`", src.GetName())
			uc.log.Warn("invalid execution command", slog.Any("error", err))
			return
		}
		srcs[src.GetName()] = src
	}

	stages := make(map[execution.StageName]any, len(command.Stages))
	for _, stage := range command.Stages {
		if _, exists := stages[stage.Name]; exists {
			err = fmt.Errorf("two or more stages have the same name '%s'", stage.Name)
			uc.log.Warn("invalid execution command", slog.Any("error", err))
			return
		}
		stages[stage.Name] = struct{}{}

		jbs := make(map[job.DefinitionName]any, len(stage.Jobs))
		for _, jb := range stage.Jobs {
			jobName := jb.GetName()
			if _, exists := jbs[jobName]; exists {
				err = fmt.Errorf("two or more jobs in stage '%s' have the same name '%s'", stage.Name, jobName)
				uc.log.Warn("invalid execution command", slog.Any("error", err))
				return
			}
			jbs[jobName] = struct{}{}
		}
	}

	var weight int64
	err = uc.unitOfWork.Do(ctx, func(ctx context.Context) error {
		stats, err := uc.calc.LoadCategoryStats(ctx, command.Stages)
		if err != nil {
			return fmt.Errorf("failed to load category stats: %w", err)
		}
		weight = uc.calc.CalculateWeight(command.Stages, stats)

		e := execution.NewExecutionDefinition(command.Stages, command.Sources, weight)
		if err = uc.executionStorage.CreateExecution(ctx, e); err != nil {
			return fmt.Errorf("failed to create execution in storage: %w", err)
		}

		result = Result{ExecutionID: e.ID}
		return nil
	})

	if err != nil {
		uc.log.Error("failed to create execution", slog.Any("error", err))
		return
	}

	uc.log.Info("created execution",
		slog.String("execution", result.ExecutionID.String()),
		slog.Int64("weight", weight),
	)

	return
}
