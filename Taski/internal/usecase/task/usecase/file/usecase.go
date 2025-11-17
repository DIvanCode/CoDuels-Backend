package file

import (
	"fmt"
	"io"
	"log/slog"
	"taski/internal/domain/task"
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
		Get(task.ID) (t task.Task, unlock func(), err error)
		GetFile(task.ID, string) (r io.ReadCloser, unlock func(), err error)
	}
)

func NewUseCase(log *slog.Logger, storage taskStorage) *UseCase {
	return &UseCase{
		log:     log,
		storage: storage,
	}
}

func (uc *UseCase) Read(query Query) (r io.ReadCloser, unlock func(), err error) {
	ok, err := uc.CheckPermissions(query.TaskID, query.File)
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

	r, unlock, err = uc.storage.GetFile(query.TaskID, query.File)
	if err != nil {
		uc.log.Error("failed to get file from storage", slog.Any("err", err))
		err = fmt.Errorf("failed to get file from storage")
		return
	}
	return
}

func (uc *UseCase) CheckPermissions(taskID task.ID, file string) (bool, error) {
	t, unlock, err := uc.storage.Get(taskID)
	if err != nil {
		return false, fmt.Errorf("failed to get task from storage: %w", err)
	}
	unlock()

	if file == t.GetStatement() {
		return true, nil
	}

	for _, test := range t.GetTests() {
		if !test.Visible {
			continue
		}

		if file == test.Input || file == test.Output {
			return true, nil
		}
	}

	return false, nil
}
