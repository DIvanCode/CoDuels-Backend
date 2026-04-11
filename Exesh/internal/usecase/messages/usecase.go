package messages

import (
	"context"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/message/history"
	"fmt"
	"log/slog"
)

type (
	UseCase struct {
		log *slog.Logger

		unitOfWork      unitOfWork
		messagesStorage messagesStorage
	}

	unitOfWork interface {
		Do(context.Context, func(ctx context.Context) error) error
	}

	messagesStorage interface {
		GetMessages(context.Context, execution.ID, int64, int) ([]history.Message, error)
	}
)

func NewUseCase(log *slog.Logger, unitOfWork unitOfWork, messagesStorage messagesStorage) *UseCase {
	return &UseCase{
		log:             log,
		unitOfWork:      unitOfWork,
		messagesStorage: messagesStorage,
	}
}

func (uc *UseCase) Get(
	ctx context.Context,
	executionID execution.ID,
	startID int64,
	count int,
) (res []history.Message, err error) {
	err = uc.unitOfWork.Do(ctx, func(ctx context.Context) error {
		if res, err = uc.messagesStorage.GetMessages(ctx, executionID, startID, count); err != nil {
			return fmt.Errorf("failed to get messages from storage: %w", err)
		}
		return nil
	})
	if err != nil {
		return nil, err
	}

	uc.log.Debug("fetched execution messages",
		slog.String("execution_id", executionID.String()),
		slog.Int64("start_id", startID),
		slog.Int("count", count),
		slog.Int("received", len(res)))

	return res, nil
}
