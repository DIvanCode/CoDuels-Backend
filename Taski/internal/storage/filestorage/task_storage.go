package filestorage

import (
	"context"
	"errors"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"taski/internal/domain/task"
	"taski/internal/domain/task/tasks"
	"time"

	"github.com/DIvanCode/filestorage/pkg/bucket"
	ferrs "github.com/DIvanCode/filestorage/pkg/errors"
)

type (
	TaskStorage struct {
		fs fileStorage
	}

	fileStorage interface {
		ListBuckets(ctx context.Context) ([]bucket.ID, error)
		GetBucket(ctx context.Context, id bucket.ID, extendTTL *time.Duration) (path string, unlock func(), err error)
		GetFile(ctx context.Context, bucketID bucket.ID, file string, extendTTL *time.Duration) (path string, unlock func(), err error)
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

func (ts *TaskStorage) Get(ctx context.Context, id task.ID) (t task.Task, unlock func(), err error) {
	bucketIDStr := id.String()
	var bucketID bucket.ID
	if err = bucketID.FromString(bucketIDStr); err != nil {
		err = fmt.Errorf("failed to convert task id to bucket id: %w", err)
		return
	}

	var path string
	path, unlock, err = ts.fs.GetBucket(ctx, bucketID, nil)
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
	defer func() {
		if err != nil {
			unlock()
		}
	}()

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

func (ts *TaskStorage) GetFile(ctx context.Context, taskID task.ID, file string) (r io.ReadCloser, unlock func(), err error) {
	bucketIDStr := taskID.String()
	var bucketID bucket.ID
	if err = bucketID.FromString(bucketIDStr); err != nil {
		err = fmt.Errorf("failed to convert task id to bucket id: %w", err)
		return
	}

	var path string
	path, unlock, err = ts.fs.GetFile(ctx, bucketID, file, nil)
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
	defer func() {
		if err != nil {
			unlock()
		}
	}()

	r, err = os.OpenFile(filepath.Join(path, file), os.O_RDONLY, 0666)
	if err != nil {
		err = fmt.Errorf("failed to open file: %w", err)
		return
	}

	return
}

func (ts *TaskStorage) GetList(ctx context.Context) ([]task.Task, error) {
	taskIDs, err := ts.GetTaskIDs(ctx)
	if err != nil {
		return nil, fmt.Errorf("failed to get task ids: %w", err)
	}

	list := make([]task.Task, len(taskIDs))
	for i, taskID := range taskIDs {
		t, unlock, err := ts.Get(ctx, taskID)
		if err != nil {
			return nil, fmt.Errorf("failed to get task from storage: %w", err)
		}
		unlock()

		list[i] = t
	}

	return list, nil
}

func (ts *TaskStorage) GetTaskIDs(ctx context.Context) ([]task.ID, error) {
	bucketIDs, err := ts.fs.ListBuckets(ctx)
	if err != nil {
		return nil, fmt.Errorf("failed to list buckets: %w", err)
	}

	taskIDs := make([]task.ID, len(bucketIDs))
	for i, bucketID := range bucketIDs {
		if err := taskIDs[i].FromString(bucketID.String()); err != nil {
			return nil, fmt.Errorf("failed to convert bucket id to task id: %w", err)
		}
	}

	return taskIDs, nil
}
