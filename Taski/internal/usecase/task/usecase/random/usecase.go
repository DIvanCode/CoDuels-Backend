package random

import (
	"fmt"
	"log/slog"
	"math/rand"
	"taski/internal/domain/task"
)

type (
	Query struct {
	}

	UseCase struct {
		log   *slog.Logger
		tasks []string
	}
)

func NewUseCase(log *slog.Logger, tasks []string) *UseCase {
	return &UseCase{log: log, tasks: tasks}
}

func (uc *UseCase) Random(query Query) (taskID task.ID, err error) {
	if err = taskID.FromString(uc.tasks[rand.Intn(len(uc.tasks))]); err != nil {
		err = fmt.Errorf("failed to parse task id: %w")
		return
	}
	return
}
