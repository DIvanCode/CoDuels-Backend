package list

import (
	"fmt"
	"log/slog"
	"taski/internal/config"
	"taski/internal/domain/task"
	"taski/internal/usecase/task/dto"
)

type (
	Query struct{}

	UseCase struct {
		log     *slog.Logger
		storage taskStorage
		tasks   config.TasksList
	}

	taskStorage interface {
		Get(task.ID) (t task.Task, unlock func(), err error)
	}
)

func NewUseCase(log *slog.Logger, storage taskStorage, tasks config.TasksList) *UseCase {
	return &UseCase{
		log:     log,
		storage: storage,
		tasks:   tasks,
	}
}

func (uc *UseCase) Get(_ Query) ([]dto.TaskDto, error) {
	tasks := make([]dto.TaskDto, len(uc.tasks))
	for i := range uc.tasks {
		var taskID task.ID
		if err := taskID.FromString(uc.tasks[i]); err != nil {
			uc.log.Error("failed to parse task id",
				slog.Any("id", uc.tasks[i]), slog.Any("err", err))
			return nil, fmt.Errorf("failed to parse task id")
		}

		t, unlock, err := uc.storage.Get(taskID)
		if err != nil {
			uc.log.Error("failed to get task from storage", slog.Any("err", err))
			return nil, fmt.Errorf("failed to get task from storage")
		}
		unlock()

		taskDto, err := dto.ConvertTask(t)
		if err != nil {
			uc.log.Error("failed to convert task to dto", slog.Any("err", err))
			return nil, fmt.Errorf("failed to convert task to dto")
		}

		tasks[i] = taskDto
	}

	return tasks, nil
}
