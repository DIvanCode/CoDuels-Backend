package random

import (
	"fmt"
	"log/slog"
	"math/rand/v2"
	"taski/internal/config"
	"taski/internal/domain/task"
)

type (
	Query struct{}

	UseCase struct {
		log   *slog.Logger
		tasks config.TasksList
	}
)

func NewUseCase(log *slog.Logger, tasks config.TasksList) *UseCase {
	return &UseCase{
		log:   log,
		tasks: tasks,
	}
}

func (uc *UseCase) Random(_ Query) (taskID task.ID, err error) {
	if err = taskID.FromString(uc.tasks[rand.N(len(uc.tasks))]); err != nil {
		uc.log.Error("failed to parse task ID", slog.Any("err", err))
		err = fmt.Errorf("failed to parse task id")
		return
	}
	return
}
