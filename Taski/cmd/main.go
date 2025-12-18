package main

import (
	"context"
	"errors"
	"fmt"
	flog "log"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	getFileAPI "taski/internal/api/task/file"
	getAPI "taski/internal/api/task/get"
	listAPI "taski/internal/api/task/list"
	randomTaskAPI "taski/internal/api/task/random"
	"taski/internal/api/testing/execute"
	testAPI "taski/internal/api/testing/test"
	"taski/internal/config"
	"taski/internal/consumer"
	"taski/internal/metrics"
	"taski/internal/producer"
	"taski/internal/storage/filestorage"
	"taski/internal/storage/postgres"
	getFileUC "taski/internal/usecase/task/usecase/file"
	getUC "taski/internal/usecase/task/usecase/get"
	listUC "taski/internal/usecase/task/usecase/list"
	randomTaskUC "taski/internal/usecase/task/usecase/random"
	testUC "taski/internal/usecase/testing/usecase/test"
	"taski/internal/usecase/testing/usecase/update"

	fs "github.com/DIvanCode/filestorage/pkg/filestorage"
	"github.com/go-chi/chi/middleware"
	"github.com/go-chi/chi/v5"
	"github.com/go-chi/cors"
	"github.com/prometheus/client_golang/prometheus"
	"github.com/prometheus/client_golang/prometheus/collectors"
	"github.com/prometheus/client_golang/prometheus/promhttp"
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
	mux.Use(middleware.Logger)
	mux.Use(cors.Handler(cors.Options{
		AllowedOrigins:   []string{"https://*", "http://*"},
		AllowedMethods:   []string{"GET", "POST", "PUT", "DELETE", "OPTIONS"},
		AllowedHeaders:   []string{"Accept", "Authorization", "Content-Type", "X-CSRF-Token"},
		ExposedHeaders:   []string{"Link"},
		AllowCredentials: false,
		MaxAge:           300,
	}))

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

	taskStorage := filestorage.NewTaskStorage(fileStorage, cfg.Tasks)

	getTaskUseCase := getUC.NewUseCase(log, taskStorage)
	getAPI.NewHandler(log, getTaskUseCase).Register(mux)

	taskListUseCase := listUC.NewUseCase(log, taskStorage)
	listAPI.NewHandler(log, taskListUseCase).Register(mux)

	randomTaskUseCase := randomTaskUC.NewUseCase(log, cfg.Tasks)
	randomTaskAPI.NewHandler(log, randomTaskUseCase).Register(mux)

	getTaskFileUseCase := getFileUC.NewUseCase(log, taskStorage)
	getFileAPI.NewHandler(log, getTaskFileUseCase).Register(mux)

	testUseCase := testUC.NewUseCase(log, taskStorage, unitOfWork, solutionStorage, executeClient, cfg.Execute.DownloadTaskEndpoint)
	testAPI.NewHandler(log, testUseCase).Register(mux)

	messageProducer := producer.NewKafkaProducer(log, cfg.MessageProducer)

	updateTestingUseCase := update.NewUseCase(log, solutionStorage, unitOfWork, taskStorage, messageProducer)

	eventConsumer := consumer.NewKafkaConsumer(log, cfg.EventConsumer, updateTestingUseCase)
	eventConsumer.Start(ctx)
	defer func() { _ = eventConsumer.Close() }()

	promRegistry := prometheus.NewRegistry()
	promRegistry.MustRegister(
		collectors.NewGoCollector(),
		collectors.NewProcessCollector(collectors.ProcessCollectorOpts{}),
	)
	promRegisterer := prometheus.WrapRegistererWithPrefix("coduels_taski_", promRegistry)

	metricsCollector := metrics.NewMetricsCollector(log, cfg.MetricsCollector, taskStorage, unitOfWork, solutionStorage)
	err = metricsCollector.RegisterMetrics(promRegisterer)
	if err != nil {
		log.Error("could not register metrics", slog.Any("err", err))
		return
	}
	metricsCollector.Start(ctx)

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
