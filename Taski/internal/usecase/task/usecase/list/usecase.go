package list

import (
	"fmt"
	"log/slog"
	"taski/internal/domain/task"
	"taski/internal/usecase/task/dto"
)

type (
	Query struct{}

	UseCase struct {
		log     *slog.Logger
		storage taskStorage
	}

	taskStorage interface {
		GetList() ([]task.Task, error)
	}
)

func NewUseCase(log *slog.Logger, storage taskStorage) *UseCase {
	return &UseCase{
		log:     log,
		storage: storage,
	}
}

func (uc *UseCase) Get(_ Query) ([]dto.TaskDto, error) {
	tasks, err := uc.storage.GetList()
	if err != nil {
		uc.log.Error("failed to get tasks list", slog.Any("err", err))
		return nil, err
	}

	tasksDto := make([]dto.TaskDto, len(tasks), len(tasks))
	for i := range tasks {
		taskDto, err := dto.ConvertTask(tasks[i])
		if err != nil {
			uc.log.Error("failed to convert task to dto", slog.Any("err", err))
			return nil, fmt.Errorf("failed to convert task to dto")
		}

		tasksDto[i] = taskDto
	}

	return tasksDto, nil
}
