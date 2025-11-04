package random

import (
	"fmt"
	"log/slog"
	"taski/internal/domain/task"
)

type (
	Query struct {
	}

	UseCase struct {
		log *slog.Logger
	}
)

func NewUseCase(log *slog.Logger) *UseCase {
	return &UseCase{log: log}
}

func (uc *UseCase) Random(query Query) (taskID task.ID, err error) {
	// temp: hardcode only one existing task id
	if err = taskID.FromString("0c5fe950363fc0aeb7d80c5fe950363fc0aeb7d8"); err != nil {
		err = fmt.Errorf("failed to parse task id: %w")
		return
	}
	return
}
