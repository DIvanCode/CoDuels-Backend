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
	r, unlock, err = uc.storage.GetFile(query.TaskID, query.File)
	if err != nil {
		uc.log.Error("failed to get file from storage", slog.Any("err", err))
		err = fmt.Errorf("failed to get file from storage: %w", err)
		return
	}
	return
}
