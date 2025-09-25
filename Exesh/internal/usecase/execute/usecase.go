package execute

import (
	"context"
	"exesh/internal/domain/execution"
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
		Create(context.Context, execution.Execution) error
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
	err = uc.unitOfWork.Do(ctx, func(ctx context.Context) error {
		e := execution.NewExecution(command.Steps)
		if err = uc.executionStorage.Create(ctx, e); err != nil {
			return fmt.Errorf("failed to create execution in storage: %w", err)
		}

		result = Result{ExecutionID: e.ID}
		return nil
	})

	uc.log.Info("created execution", slog.Any("execution_id", result.ExecutionID))

	return
}
