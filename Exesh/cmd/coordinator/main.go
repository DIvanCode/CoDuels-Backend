package main

import (
	"context"
	executeAPI "exesh/internal/api/execute"
	"exesh/internal/config"
	"exesh/internal/storage/postgres"
	executeUC "exesh/internal/usecase/execute"
	"fmt"
	flog "log"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"

	"github.com/go-chi/chi/v5"
)

func main() {
	cfg := config.MustLoadCoordinatorConfig()

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

	unitOfWork, executionStorage, err := setupStorage(log, cfg.Storage)
	if err != nil {
		log.Error("failed to setup storage", slog.String("error", err.Error()))
		return
	}

	executeUseCase := executeUC.NewUseCase(log, unitOfWork, executionStorage)
	executeAPI.NewHandler(log, executeUseCase).Register(mux)

	log.Info("starting server", slog.String("address", cfg.HttpServer.Addr))

	done := make(chan os.Signal, 1)
	signal.Notify(done, os.Interrupt, syscall.SIGINT, syscall.SIGTERM)

	srv := &http.Server{
		Addr:    cfg.HttpServer.Addr,
		Handler: mux,
	}

	go func() {
		_ = srv.ListenAndServe()
	}()

	log.Info("server started")

	<-done
	log.Info("stopping server")

	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

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

func setupStorage(
	log *slog.Logger,
	cfg config.StorageConfig,
) (
	unitOfWork *postgres.UnitOfWork,
	executionStorage *postgres.ExecutionStorage,
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
		if executionStorage, err = postgres.NewExecutionStorage(ctx, log); err != nil {
			return fmt.Errorf("failed to create execution storage: %w", err)
		}
		return nil
	})

	return
}
