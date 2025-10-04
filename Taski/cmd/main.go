package main

import (
	"context"
	"fmt"
	flog "log"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	getFileAPI "taski/internal/api/task/file"
	getAPI "taski/internal/api/task/get"
	"taski/internal/api/testing/execute"
	testAPI "taski/internal/api/testing/test"
	"taski/internal/config"
	"taski/internal/storage/filestorage"
	"taski/internal/storage/postgres"
	getFileUC "taski/internal/usecase/task/usecase/file"
	getUC "taski/internal/usecase/task/usecase/get"
	testUC "taski/internal/usecase/testing/usecase/test"

	fs "github.com/DIvanCode/filestorage/pkg/filestorage"
	"github.com/go-chi/chi/v5"
)

func main() {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	cfg := config.MustLoad()

	log, err := setupLogger(cfg.Env)
	if err != nil {
		flog.Fatal(err)
	}

	log.Info(
		"starting coordinator",
		slog.String("env", cfg.Env),
	)
	log.Debug("debug messages are enabled")

	mux := chi.NewRouter()

	fileStorage, err := fs.New(log, cfg.FileStorage, mux)
	if err != nil {
		log.Error("failed to create filestorage", slog.String("error", err.Error()))
		return
	}
	defer fileStorage.Shutdown()

	unitOfWork, solutionStorage, err := setupDb(log, cfg.Db)
	if err != nil {
		log.Error("failed to setup db", slog.String("error", err.Error()))
		return
	}

	executeClient := execute.NewExecuteClient(log, cfg.Execute.Endpoint)

	taskStorage := filestorage.NewTaskStorage(fileStorage)

	getTaskUseCase := getUC.NewUseCase(log, taskStorage)
	getAPI.NewHandler(log, getTaskUseCase).Register(mux)

	getTaskFileUseCase := getFileUC.NewUseCase(log, taskStorage)
	getFileAPI.NewHandler(log, getTaskFileUseCase).Register(mux)

	testUseCase := testUC.NewUseCase(log, taskStorage, unitOfWork, solutionStorage, executeClient, cfg.HttpServer.Addr)
	testAPI.NewHandler(log, testUseCase).Register(mux)

	log.Info("starting server", slog.String("address", cfg.HttpServer.Addr))

	stop := make(chan os.Signal, 1)
	signal.Notify(stop, os.Interrupt, syscall.SIGINT, syscall.SIGTERM)

	srv := &http.Server{
		Addr:    cfg.HttpServer.Addr,
		Handler: mux,
	}

	go func() {
		_ = srv.ListenAndServe()
	}()

	log.Info("server started")

	<-stop
	log.Info("stopping server")

	if err := srv.Shutdown(ctx); err != nil {
		log.Error("failed to stop server", slog.Any("error", err))
		return
	}

	log.Info("server stopped")
}

func setupLogger(env string) (log *slog.Logger, err error) {
	switch env {
	case "dev":
		log = slog.New(slog.NewTextHandler(os.Stdout, &slog.HandlerOptions{Level: slog.LevelDebug}))
	default:
		err = fmt.Errorf("failed setup logger for env %s", env)
	}

	return
}

func setupDb(log *slog.Logger, cfg config.DbConfig) (
	unitOfWork *postgres.UnitOfWork,
	solutionStorage *postgres.SolutionStorage,
	err error,
) {
	ctx, cancel := context.WithTimeout(context.Background(), cfg.InitTimeout)
	defer cancel()

	unitOfWork, err = postgres.NewUnitOfWork(cfg)
	if err != nil {
		err = fmt.Errorf("failed to create unit of work: %w", err)
		return
	}

	err = unitOfWork.Do(ctx, func(ctx context.Context) error {
		if solutionStorage, err = postgres.NewSolutionStorage(ctx, log); err != nil {
			return fmt.Errorf("failed to create solution storage: %w", err)
		}
		return nil
	})

	return
}
