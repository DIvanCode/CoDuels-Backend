package filestorage

import (
	"errors"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"taski/internal/domain/task"
	"taski/internal/domain/task/tasks"

	"github.com/DIvanCode/filestorage/pkg/bucket"
	ferrs "github.com/DIvanCode/filestorage/pkg/errors"
)

type (
	TaskStorage struct {
		fs fileStorage
	}

	fileStorage interface {
		GetBucket(id bucket.ID) (path string, unlock func(), err error)
		GetFile(bucketID bucket.ID, file string) (path string, unlock func(), err error)
	}
)

func NewTaskStorage(fs fileStorage) *TaskStorage {
	return &TaskStorage{
		fs: fs,
	}
}

func (ts *TaskStorage) GetTaskBucket(id task.ID) (bucketID bucket.ID, err error) {
	bucketIDStr := id.String()
	if err = bucketID.FromString(bucketIDStr); err != nil {
		err = fmt.Errorf("failed to convert task id to bucket id: %w", err)
		return
	}
	return
}

func (ts *TaskStorage) Get(id task.ID) (t task.Task, unlock func(), err error) {
	bucketIDStr := id.String()
	var bucketID bucket.ID
	if err = bucketID.FromString(bucketIDStr); err != nil {
		err = fmt.Errorf("failed to convert task id to bucket id: %w", err)
		return
	}

	var path string
	path, unlock, err = ts.fs.GetBucket(bucketID)
	if err != nil {
		if errors.Is(err, ferrs.ErrBucketNotFound) {
			err = fmt.Errorf("task bucket not found")
			return
		}
		if errors.Is(err, ferrs.ErrWriteLocked) {
			err = fmt.Errorf("task bucket write locked")
			return
		}
		err = fmt.Errorf("failed to get bucket: %w", err)
		return
	}

	f, err := os.OpenFile(filepath.Join(path, "task.json"), os.O_RDONLY, 0666)
	if err != nil {
		err = fmt.Errorf("failed to open task file: %w", err)
		return
	}
	defer func() { _ = f.Close() }()

	data, err := io.ReadAll(f)
	if err != nil {
		err = fmt.Errorf("failed to read task file: %w", err)
		return
	}

	t, err = tasks.UnmarshalTaskJSON(data)
	if err != nil {
		err = fmt.Errorf("failed to unmarshal task: %w", err)
		return
	}

	return
}

func (ts *TaskStorage) GetFile(taskID task.ID, file string) (r io.ReadCloser, unlock func(), err error) {
	bucketIDStr := taskID.String()
	var bucketID bucket.ID
	if err = bucketID.FromString(bucketIDStr); err != nil {
		err = fmt.Errorf("failed to convert task id to bucket id: %w", err)
		return
	}

	var path string
	path, unlock, err = ts.fs.GetFile(bucketID, file)
	if err != nil {
		if errors.Is(err, ferrs.ErrBucketNotFound) {
			err = fmt.Errorf("task bucket not found")
			return
		}
		if errors.Is(err, ferrs.ErrFileNotFound) {
			err = fmt.Errorf("file in bucket not found")
			return
		}
		if errors.Is(err, ferrs.ErrWriteLocked) {
			err = fmt.Errorf("task bucket write locked")
			return
		}
		err = fmt.Errorf("failed to get file from bucket: %w", err)
		return
	}

	r, err = os.OpenFile(filepath.Join(path, file), os.O_RDONLY, 0666)
	if err != nil {
		err = fmt.Errorf("failed to open file: %w", err)
		return
	}

	return
}
