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
		log     *slog.Logger
	}
)

func NewUseCase(log *slog.Logger) *UseCase {
	return &UseCase{log: log}
}

func (uc *UseCase) Random(query Query) (taskID task.ID, err error) {
	// temp: hardcode only one existing task id
	if err = taskID.FromString("7d971f50363cf0aebbd87d971f50363cf0aebbd8"); err != nil {
		err = fmt.Errorf("failed to parse task id: %w", err)
		return
	}
	return
}
