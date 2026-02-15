package file

import (
	"context"
	"fmt"
	"io"
	"log/slog"
	"taski/internal/domain/task"
	"taski/internal/domain/task/tasks"
)

type (
	Query struct {
		TaskID task.ID
		File   string
	}

	UseCase struct {
		log     *slog.Logger
		storage taskStorage
	}

	taskStorage interface {
		Get(context.Context, task.ID) (t task.Task, unlock func(), err error)
		GetFile(context.Context, task.ID, string) (r io.ReadCloser, unlock func(), err error)
	}
)

func NewUseCase(log *slog.Logger, storage taskStorage) *UseCase {
	return &UseCase{
		log:     log,
		storage: storage,
	}
}

func (uc *UseCase) Read(ctx context.Context, query Query) (r io.ReadCloser, unlock func(), err error) {
	ok, err := uc.CheckPermissions(ctx, query.TaskID, query.File)
	if err != nil {
		uc.log.Error("failed to check permissions",
			slog.Any("taskID", query.TaskID),
			slog.Any("file", query.File),
			slog.Any("err", err))
		err = fmt.Errorf("failed to check permissions")
		return
	}

	if !ok {
		err = fmt.Errorf("permission denied")
		return
	}

	r, unlock, err = uc.storage.GetFile(ctx, query.TaskID, query.File)
	if err != nil {
		uc.log.Error("failed to get file from storage", slog.Any("err", err))
		err = fmt.Errorf("failed to get file from storage")
		return
	}
	return
}

func (uc *UseCase) CheckPermissions(ctx context.Context, taskID task.ID, file string) (bool, error) {
	t, unlock, err := uc.storage.Get(ctx, taskID)
	if err != nil {
		return false, fmt.Errorf("failed to get task from storage: %w", err)
	}
	unlock()

	if file == t.GetStatement() {
		return true, nil
	}

	switch t.GetType() {
	case task.WriteCode:
		typedTask := t.(*tasks.WriteCodeTask)

		if typedTask.SourceCode != nil && file == typedTask.SourceCode.Path {
			return true, nil
		}

		for _, test := range typedTask.Tests {
			if !test.Visible {
				continue
			}

			if file == test.Input || file == test.Output {
				return true, nil
			}
		}
	case task.PredictOutput:
		typedTask := t.(*tasks.PredictOutputTask)

		if file == typedTask.Code.Path {
			return true, nil
		}
	case task.FindTest:
		typedTask := t.(*tasks.PredictOutputTask)

		if file == typedTask.Code.Path {
			return true, nil
		}
	}

	return false, nil
}
