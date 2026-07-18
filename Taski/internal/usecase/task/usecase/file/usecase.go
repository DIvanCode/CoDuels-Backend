package file

import (
	"context"
	"errors"
	"fmt"
	"io"
	"log/slog"
	"taski/internal/domain/task"
	"taski/internal/domain/task/tasks"
	"taski/internal/lib/safepath"
)

var (
	ErrInvalidPath = errors.New("invalid file path")
	ErrForbidden   = errors.New("task file access forbidden")
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
	cleanFile, err := cleanRequestedFile(query.File)
	if err != nil {
		return nil, nil, err
	}

	ok, err := uc.checkPermissions(ctx, query.TaskID, cleanFile)
	if err != nil {
		return nil, nil, err
	}

	if !ok {
		return nil, nil, ErrForbidden
	}

	r, unlock, err = uc.storage.GetFile(ctx, query.TaskID, cleanFile)
	if err != nil {
		if errors.Is(err, task.ErrNotFound) || errors.Is(err, task.ErrFileNotFound) {
			return nil, nil, err
		}
		uc.log.Error("failed to get task file from storage",
			slog.Any("task_id", query.TaskID),
			slog.String("file", cleanFile),
			slog.Any("error", err))
		return nil, nil, fmt.Errorf("get task file from storage: %w", err)
	}
	return r, unlock, nil
}

func (uc *UseCase) CheckPermissions(ctx context.Context, taskID task.ID, file string) (bool, error) {
	cleanFile, err := cleanRequestedFile(file)
	if err != nil {
		return false, err
	}
	return uc.checkPermissions(ctx, taskID, cleanFile)
}

func (uc *UseCase) checkPermissions(ctx context.Context, taskID task.ID, cleanFile string) (bool, error) {
	t, unlock, err := uc.storage.Get(ctx, taskID)
	if err != nil {
		if errors.Is(err, task.ErrNotFound) {
			return false, err
		}
		uc.log.Error("failed to get task from storage",
			slog.Any("task_id", taskID),
			slog.Any("error", err))
		return false, fmt.Errorf("get task from storage: %w", err)
	}
	defer unlock()

	allowedFiles, err := publicFiles(t)
	if err != nil {
		uc.log.Error("failed to build public task file allowlist",
			slog.Any("task_id", taskID),
			slog.Any("error", err))
		return false, fmt.Errorf("build public task file allowlist: %w", err)
	}
	_, ok := allowedFiles[cleanFile]
	return ok, nil
}

func cleanRequestedFile(file string) (string, error) {
	clean, err := safepath.Clean(file)
	if err != nil {
		return "", ErrInvalidPath
	}
	return clean, nil
}

func publicFiles(t task.Task) (map[string]struct{}, error) {
	if t == nil {
		return nil, errors.New("nil task")
	}

	files := make(map[string]struct{})
	add := func(kind, file string) error {
		clean, err := safepath.Clean(file)
		if err != nil {
			return fmt.Errorf("invalid %s path %q: %w", kind, file, err)
		}
		files[clean] = struct{}{}
		return nil
	}

	if err := add("statement", t.GetStatement()); err != nil {
		return nil, err
	}

	switch t.GetType() {
	case task.WriteCode:
		typedTask, ok := t.(*tasks.WriteCodeTask)
		if !ok {
			return nil, fmt.Errorf("task type %q has value %T", task.WriteCode, t)
		}
		if typedTask.SourceCode != nil {
			if err := add("source code", typedTask.SourceCode.Path); err != nil {
				return nil, err
			}
		}
		for _, test := range typedTask.Tests {
			if !test.Visible {
				continue
			}
			if err := add("visible test input", test.Input); err != nil {
				return nil, err
			}
			if err := add("visible test output", test.Output); err != nil {
				return nil, err
			}
		}
	case task.PredictOutput:
		typedTask, ok := t.(*tasks.PredictOutputTask)
		if !ok {
			return nil, fmt.Errorf("task type %q has value %T", task.PredictOutput, t)
		}
		if err := add("code", typedTask.Code.Path); err != nil {
			return nil, err
		}
		if err := add("test input", typedTask.Test.Input); err != nil {
			return nil, err
		}
	case task.FindTest:
		typedTask, ok := t.(*tasks.FindTestTask)
		if !ok {
			return nil, fmt.Errorf("task type %q has value %T", task.FindTest, t)
		}
		if err := add("code", typedTask.Code.Path); err != nil {
			return nil, err
		}
	default:
		return nil, fmt.Errorf("unsupported task type %q", t.GetType())
	}

	return files, nil
}
