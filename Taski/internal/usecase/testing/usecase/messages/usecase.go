package messages

import (
	"context"
	"fmt"
	"log/slog"
	"taski/internal/domain/testing"
	"taski/internal/domain/testing/message/history"
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
		GetMessages(context.Context, testing.ExternalSolutionID, int64, int) ([]history.Message, error)
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
	solutionID testing.ExternalSolutionID,
	startID int64,
	count int,
) (res []history.Message, err error) {
	err = uc.unitOfWork.Do(ctx, func(ctx context.Context) error {
		if res, err = uc.messagesStorage.GetMessages(ctx, solutionID, startID, count); err != nil {
			return fmt.Errorf("failed to get messages from storage: %w", err)
		}
		return nil
	})
	if err != nil {
		return nil, err
	}

	uc.log.Debug("fetched testing messages",
		slog.String("solution_id", string(solutionID)),
		slog.Int64("start_id", startID),
		slog.Int("count", count),
		slog.Int("received", len(res)))

	return res, nil
}
