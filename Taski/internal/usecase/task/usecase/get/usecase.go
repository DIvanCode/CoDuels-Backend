package get

import (
	"fmt"
	"log/slog"
	"taski/internal/domain/task"
	dto "taski/internal/usecase/task/dto"
)

type (
	Query struct {
		TaskID task.ID
	}

	UseCase struct {
		log     *slog.Logger
		storage taskStorage
	}

	taskStorage interface {
		Get(task.ID) (t task.Task, unlock func(), err error)
	}
)

func NewUseCase(log *slog.Logger, storage taskStorage) *UseCase {
	return &UseCase{
		log:     log,
		storage: storage,
	}
}

func (uc *UseCase) Get(query Query) (dto.TaskDto, error) {
	t, unlock, err := uc.storage.Get(query.TaskID)
	if err != nil {
		uc.log.Error("failed to get task from storage", slog.Any("err", err))
		return nil, fmt.Errorf("failed to get task from storage")
	}
	defer unlock()

	taskDto, err := dto.ConvertTask(t)
	if err != nil {
		uc.log.Error("failed to convert task", slog.Any("err", err))
		return nil, fmt.Errorf("failed to convert task")
	}

	return taskDto, nil
}
