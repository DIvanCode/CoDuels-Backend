package main

import (
	"context"
	"errors"
	executeAPI "exesh/internal/api/execute"
	heartbeatAPI "exesh/internal/api/heartbeat"
	messagesAPI "exesh/internal/api/messages"
	"exesh/internal/calculator"
	"exesh/internal/config"
	"exesh/internal/dispatcher"
	"exesh/internal/factory"
	"exesh/internal/provider/adapter"
	schedule "exesh/internal/scheduler"
	"exesh/internal/storage/postgres"
	executeUC "exesh/internal/usecase/execute"
	heartbeatUC "exesh/internal/usecase/heartbeat"
	messagesUC "exesh/internal/usecase/messages"
	"fmt"
	flog "log"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"

	"github.com/DIvanCode/filestorage/pkg/filestorage"
	"github.com/go-chi/chi/v5"
	"github.com/prometheus/client_golang/prometheus"
	"github.com/prometheus/client_golang/prometheus/collectors"
	"github.com/prometheus/client_golang/prometheus/promhttp"
)

func main() {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

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

	unitOfWork, executionStorage, outboxStorage, messageStorage, categoryHistogramStorage, err := setupStorage(log, cfg.Storage)
	if err != nil {
		log.Error("failed to setup storage", slog.String("error", err.Error()))
		return
	}

	fs, err := filestorage.New(log, cfg.FileStorage.ToExternal(), mux)
	if err != nil {
		log.Error("failed to create filestorage", slog.String("error", err.Error()))
		return
	}
	defer fs.Shutdown()

	workerPool := schedule.NewWorkerPool(log, cfg.WorkerPool)
	workerPool.StartObserver(ctx)

	filestorageAdapter := adapter.NewFilestorageAdapter(fs)
	calc := calculator.NewCalculator(categoryHistogramStorage)
	executionFactory := factory.NewExecutionFactory(cfg.JobFactory, filestorageAdapter, calc)

	messageFactory := factory.NewMessageFactory()
	messageDispatcher := dispatcher.NewMessageDispatcher(log, cfg.Dispatcher, unitOfWork, outboxStorage, messageStorage)
	messageDispatcher.Start(ctx)

	promRegistry := prometheus.NewRegistry()
	promRegistry.MustRegister(
		collectors.NewGoCollector(),
		collectors.NewProcessCollector(collectors.ProcessCollectorOpts{}),
	)
	promCoordinatorRegistry := prometheus.WrapRegistererWithPrefix("coduels_exesh_coordinator_", promRegistry)

	executionScheduler := schedule.NewExecutionScheduler(log, cfg.ExecutionScheduler,
		unitOfWork, executionStorage, categoryHistogramStorage,
		executionFactory, workerPool, messageFactory, messageDispatcher)
	jobScheduler := schedule.NewJobScheduler(log, cfg.JobScheduler, workerPool, executionScheduler)

	err = executionScheduler.RegisterMetrics(promCoordinatorRegistry)
	if err != nil {
		log.Error("could not register metrics from execution scheduler", slog.Any("err", err))
		return
	}

	executionScheduler.Start(ctx)

	executeUseCase := executeUC.NewUseCase(log, unitOfWork, executionStorage, calc)
	executeAPI.NewHandler(log, executeUseCase).Register(mux)

	heartbeatUseCase := heartbeatUC.NewUseCase(log, workerPool, jobScheduler)
	heartbeatAPI.NewHandler(log, heartbeatUseCase).Register(mux)

	messagesUseCase := messagesUC.NewUseCase(log, unitOfWork, messageStorage)
	messagesAPI.NewHandler(log, messagesUseCase).Register(mux)

	log.Info("starting server", slog.String("address", cfg.HttpServer.Addr))

	stop := make(chan os.Signal, 1)
	signal.Notify(stop, os.Interrupt, syscall.SIGINT, syscall.SIGTERM)

	srv := &http.Server{
		Addr:    cfg.HttpServer.Addr,
		Handler: mux,
	}

	msrv := &http.Server{
		Addr:    cfg.HttpServer.MetricsAddr,
		Handler: promhttp.HandlerFor(promRegistry, promhttp.HandlerOpts{}),
	}

	go func() {
		_ = srv.ListenAndServe()
	}()

	go func() {
		if cfg.HttpServer.MetricsAddr != "" {
			_ = msrv.ListenAndServe()
		}
	}()

	log.Info("server started")

	<-stop
	log.Info("stopping server")

	if err := errors.Join(srv.Shutdown(ctx), msrv.Shutdown(ctx)); err != nil {
		log.Error("failed to stop server", slog.Any("error", err))
		return
	}

	log.Info("server stopped")
}

func setupLogger(env string) (log *slog.Logger, err error) {
	switch env {
	case "dev":
		log = slog.New(slog.NewTextHandler(os.Stdout, &slog.HandlerOptions{Level: slog.LevelDebug}))
	case "docker":
		log = slog.New(slog.NewTextHandler(os.Stdout, &slog.HandlerOptions{Level: slog.LevelDebug}))
	case "prod":
		log = slog.New(slog.NewTextHandler(os.Stdout, &slog.HandlerOptions{Level: slog.LevelInfo}))
	default:
		err = fmt.Errorf("failed setup logger for env %s", env)
	}

	return log, err
}

func setupStorage(log *slog.Logger, cfg config.StorageConfig) (
	unitOfWork *postgres.UnitOfWork,
	executionStorage *postgres.ExecutionStorage,
	outboxStorage *postgres.OutboxStorage,
	messageStorage *postgres.MessageStorage,
	categoryHistogramStorage *postgres.CategoryHistogramStorage,
	err error,
) {
	ctx, cancel := context.WithTimeout(context.Background(), cfg.InitTimeout)
	defer cancel()

	unitOfWork, err = postgres.NewUnitOfWork(cfg)
	if err != nil {
		err = fmt.Errorf("failed to create unit of work: %w", err)
		return unitOfWork, executionStorage, outboxStorage, messageStorage, categoryHistogramStorage, err
	}

	err = unitOfWork.Do(ctx, func(ctx context.Context) error {
		if executionStorage, err = postgres.NewExecutionStorage(ctx, log); err != nil {
			return fmt.Errorf("failed to create execution storage: %w", err)
		}
		if outboxStorage, err = postgres.NewOutboxStorage(ctx, log); err != nil {
			return fmt.Errorf("failed to create outbox storage: %w", err)
		}
		if messageStorage, err = postgres.NewMessageStorage(ctx, log); err != nil {
			return fmt.Errorf("failed to create message storage: %w", err)
		}
		if categoryHistogramStorage, err = postgres.NewCategoryHistogramStorage(ctx, log); err != nil {
			return fmt.Errorf("failed to create category histogram storage: %w", err)
		}
		return nil
	})

	return unitOfWork, executionStorage, outboxStorage, messageStorage, categoryHistogramStorage, err
}
