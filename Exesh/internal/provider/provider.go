package provider

import (
	"context"
	"github.com/DIvanCode/filestorage/pkg/bucket"
	"io"
	"time"
)

type filestorage interface {
	DownloadBucket(context.Context, bucket.ID, time.Duration, string) error
	DownloadFile(context.Context, bucket.ID, string, time.Duration, string) error
	CreateFile(context.Context, bucket.ID, string, time.Duration) (io.Writer, func() error, func() error, error)
	ReserveFile(context.Context, bucket.ID, string, time.Duration) (string, func() error, func() error, error)
	ReadFile(context.Context, bucket.ID, string, time.Duration) (io.Reader, func(), error)
	LocateFile(context.Context, bucket.ID, string, time.Duration) (string, func(), error)
}
