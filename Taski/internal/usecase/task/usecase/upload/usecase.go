package upload

import (
	"context"
	"log/slog"
	"taski/internal/domain/task"
	"taski/internal/uploader"
	"taski/internal/uploader/polygon"

	fs "github.com/DIvanCode/filestorage/pkg/filestorage"
)

const (
	FormatPolygon = uploader.FormatPolygon
)

type UseCase struct {
	log        *slog.Logger
	dispatcher *uploader.Dispatcher
}

type Command struct {
	Format  string
	SrcPath string
	Level   int
}

func NewUseCase(log *slog.Logger, fileStorage fs.FileStorage) *UseCase {
	return &UseCase{
		log:        log,
		dispatcher: uploader.NewDispatcher(polygon.NewUploader(fileStorage, log)),
	}
}

func (uc *UseCase) Upload(ctx context.Context, command Command) (task.ID, error) {
	taskID, err := uc.dispatcher.Upload(ctx, uploader.Config{
		Format:  command.Format,
		SrcPath: command.SrcPath,
		Level:   command.Level,
	})
	if err != nil {
		return task.ID{}, err
	}

	uc.log.Info("polygon package converted",
		slog.String("task_id", taskID.String()),
	)

	return taskID, nil
}
