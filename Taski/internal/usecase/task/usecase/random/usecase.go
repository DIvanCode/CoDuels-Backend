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
	if err = taskID.FromString("c611ca79dbe5d03a3e103a45f85ca097167b0a7e"); err != nil {
		err = fmt.Errorf("failed to parse task id: %w")
		return
	}
	return
}
