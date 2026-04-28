package main

import (
	"context"
	"flag"
	"fmt"
	"io"
	"log/slog"
	"os"
	"os/signal"
	"syscall"
	"taski/internal/config"
	"taski/internal/usecase/task/usecase/upload"

	fs "github.com/DIvanCode/filestorage/pkg/filestorage"
	"github.com/go-chi/chi/v5"
)

type syncWriter struct {
	w io.Writer
}

func (s syncWriter) Write(p []byte) (int, error) {
	n, err := s.w.Write(p)
	if syncer, ok := s.w.(interface{ Sync() error }); ok {
		_ = syncer.Sync()
	}
	return n, err
}

func main() {
	os.Exit(run())
}

func run() int {
	ctx, cancel := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGINT, syscall.SIGTERM)
	defer cancel()

	log := slog.New(slog.NewTextHandler(syncWriter{w: os.Stdout}, &slog.HandlerOptions{Level: slog.LevelInfo}))
	cfg := config.MustLoadUploader()

	mux := chi.NewRouter()
	fileStorage, err := fs.New(log, cfg.FileStorage, mux)
	if err != nil {
		fmt.Fprintln(os.Stderr, "failed to create filestorage:", err)
		return 1
	}
	defer fileStorage.Shutdown()

	uc := upload.NewUseCase(log, fileStorage)

	var command upload.Command
	flag.StringVar(&command.Format, "format", upload.FormatPolygon, "source task format")
	flag.StringVar(&command.SrcPath, "src", "", "path to polygon package directory or zip archive")
	flag.IntVar(&command.Level, "level", 1, "task level [1..10]")
	flag.Parse()

	taskID, err := uc.Upload(ctx, command)
	if err != nil {
		fmt.Fprintln(os.Stderr, "uploader error:", err)
		return 1
	}

	fmt.Printf("Task ID: %s\n", taskID.String())
	return 0
}
