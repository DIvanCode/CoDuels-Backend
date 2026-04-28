package random

import (
	"context"
	"fmt"
	"log/slog"
	"math/rand/v2"
	"taski/internal/domain/task"
)

type (
	Query struct{}

	UseCase struct {
		log     *slog.Logger
		storage taskStorage
	}

	taskStorage interface {
		GetTaskIDs(context.Context) ([]task.ID, error)
	}
)

func NewUseCase(log *slog.Logger, storage taskStorage) *UseCase {
	return &UseCase{
		log:     log,
		storage: storage,
	}
}

func (uc *UseCase) Random(ctx context.Context, _ Query) (taskID task.ID, err error) {
	taskIDs, err := uc.storage.GetTaskIDs(ctx)
	if err != nil {
		uc.log.Error("failed to get task ids", slog.Any("err", err))
		err = fmt.Errorf("failed to get task ids")
		return
	}

	if len(taskIDs) == 0 {
		err = fmt.Errorf("no tasks found")
		return
	}

	taskID = taskIDs[rand.N(len(taskIDs))]
	return
}
