package filestorage

import (
	"context"
	"errors"
	"strings"
	"testing"
	"time"

	"taski/internal/domain/task"

	"github.com/DIvanCode/filestorage/pkg/bucket"
	ferrs "github.com/DIvanCode/filestorage/pkg/errors"
)

func TestTaskStorageClassifiesNotFoundErrors(t *testing.T) {
	t.Parallel()

	tests := []struct {
		name    string
		storage *stubFileStorage
		read    func(*TaskStorage) error
		wantErr error
	}{
		{
			name:    "task bucket",
			storage: &stubFileStorage{getBucketErr: ferrs.ErrBucketNotFound},
			read: func(storage *TaskStorage) error {
				_, _, err := storage.Get(context.Background(), storageTaskID(t))
				return err
			},
			wantErr: task.ErrNotFound,
		},
		{
			name:    "file bucket",
			storage: &stubFileStorage{getFileErr: ferrs.ErrBucketNotFound},
			read: func(storage *TaskStorage) error {
				_, _, err := storage.GetFile(context.Background(), storageTaskID(t), "statement.html")
				return err
			},
			wantErr: task.ErrNotFound,
		},
		{
			name:    "bucket file",
			storage: &stubFileStorage{getFileErr: ferrs.ErrFileNotFound},
			read: func(storage *TaskStorage) error {
				_, _, err := storage.GetFile(context.Background(), storageTaskID(t), "statement.html")
				return err
			},
			wantErr: task.ErrFileNotFound,
		},
		{
			name:    "local file disappeared",
			storage: &stubFileStorage{filePath: t.TempDir()},
			read: func(storage *TaskStorage) error {
				_, _, err := storage.GetFile(context.Background(), storageTaskID(t), "statement.html")
				return err
			},
			wantErr: task.ErrFileNotFound,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			t.Parallel()

			err := tt.read(NewTaskStorage(tt.storage))
			if !errors.Is(err, tt.wantErr) {
				t.Fatalf("error = %v, want %v", err, tt.wantErr)
			}
		})
	}
}

type stubFileStorage struct {
	getBucketErr error
	getFileErr   error
	filePath     string
}

func (s *stubFileStorage) ListBuckets(context.Context) ([]bucket.ID, error) {
	return nil, nil
}

func (s *stubFileStorage) GetBucket(context.Context, bucket.ID, *time.Duration) (string, func(), error) {
	return "", func() {}, s.getBucketErr
}

func (s *stubFileStorage) GetFile(context.Context, bucket.ID, string, *time.Duration) (string, func(), error) {
	return s.filePath, func() {}, s.getFileErr
}

func storageTaskID(t *testing.T) task.ID {
	t.Helper()

	var id task.ID
	if err := id.FromString(strings.Repeat("a", 40)); err != nil {
		t.Fatalf("create task id: %v", err)
	}
	return id
}
