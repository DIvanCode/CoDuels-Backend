package uploader

import (
	"context"
	"fmt"
	"taski/internal/domain/task"
)

const (
	FormatPolygon = "polygon"
)

type Config struct {
	Format  string
	SrcPath string
	Level   int
}

type Uploader interface {
	SupportsFormat(format string) bool
	Upload(ctx context.Context, cfg Config) (task.ID, error)
}

type Dispatcher struct {
	uploaders []Uploader
}

func NewDispatcher(uploaders ...Uploader) *Dispatcher {
	return &Dispatcher{uploaders: uploaders}
}

func (d *Dispatcher) Upload(ctx context.Context, cfg Config) (task.ID, error) {
	for _, up := range d.uploaders {
		if up.SupportsFormat(cfg.Format) {
			return up.Upload(ctx, cfg)
		}
	}
	return task.ID{}, fmt.Errorf("unsupported format: %s", cfg.Format)
}
