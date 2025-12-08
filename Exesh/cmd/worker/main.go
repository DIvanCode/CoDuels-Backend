package main

import (
	"context"
	"errors"
	"exesh/internal/config"
	"exesh/internal/executor"
	"exesh/internal/executor/executors"
	"exesh/internal/provider"
	"exesh/internal/provider/providers"
	"exesh/internal/provider/providers/adapter"
	"exesh/internal/runtime/docker"
	"exesh/internal/worker"
	"fmt"
	flog "log"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"

	"github.com/DIvanCode/filestorage/pkg/filestorage"
	"github.com/go-chi/chi/v5"
	"github.com/prometheus/client_golang/prometheus/promhttp"
)

func main() {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	cfg := config.MustLoadWorkerConfig()

	log, err := setupLogger(cfg.Env)
	if err != nil {
		flog.Fatal(err)
	}

	log.Info(
		"starting worker",
		slog.String("env", cfg.Env),
	)
	log.Debug("debug messages are enabled")

	mux := chi.NewRouter()

	filestorage, err := filestorage.New(log, cfg.FileStorage, mux)
	if err != nil {
		log.Error("failed to create filestorage", slog.String("error", err.Error()))
		return
	}
	defer filestorage.Shutdown()

	filestorageAdapter := adapter.NewFilestorageAdapter(filestorage)
	inputProvider := setupInputProvider(cfg.InputProvider, filestorageAdapter)
	outputProvider := setupOutputProvider(cfg.OutputProvider, filestorageAdapter)

	jobExecutor, err := setupJobExecutor(log, inputProvider, outputProvider)
	if err != nil {
		flog.Fatal(err)
	}

	worker.NewWorker(log, cfg.Worker, jobExecutor).Start(ctx)

	log.Info("starting server", slog.String("address", cfg.HttpServer.Addr))

	stop := make(chan os.Signal, 1)
	signal.Notify(stop, os.Interrupt, syscall.SIGINT, syscall.SIGTERM)

	srv := &http.Server{
		Addr:    cfg.HttpServer.Addr,
		Handler: mux,
	}

	msrv := &http.Server{
		Addr:    cfg.HttpServer.MetricsAddr,
		Handler: promhttp.Handler(),
	}

	go func() {
		_ = srv.ListenAndServe()
	}()

	go func() {
		_ = msrv.ListenAndServe()
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

	return log, err
}

func setupInputProvider(cfg config.InputProviderConfig, filestorageAdapter *adapter.FilestorageAdapter) *provider.InputProvider {
	filestorageBucketInputProvider := providers.NewFilestorageBucketInputProvider(filestorageAdapter, cfg.FilestorageBucketTTL)
	artifactInputProvider := providers.NewArtifactInputProvider(filestorageAdapter, cfg.ArtifactTTL)
	return provider.NewInputProvider(filestorageBucketInputProvider, artifactInputProvider)
}

func setupOutputProvider(cfg config.OutputProviderConfig, filestorageAdapter *adapter.FilestorageAdapter) *provider.OutputProvider {
	artifactOutputProvider := providers.NewArtifactOutputProvider(filestorageAdapter, cfg.ArtifactTTL)
	return provider.NewOutputProvider(artifactOutputProvider)
}

func setupJobExecutor(log *slog.Logger, inputProvider *provider.InputProvider, outputProvider *provider.OutputProvider) (*executor.JobExecutor, error) {
	gccRT, err := docker.New(
		docker.WithDefaultClient(),
		docker.WithBaseImage("gcc"),
		docker.WithRestrictivePolicy(),
	)
	if err != nil {
		return nil, fmt.Errorf("create cpp runtime: %w", err)
	}
	goRT, err := docker.New(
		docker.WithDefaultClient(),
		docker.WithBaseImage("golang"),
		docker.WithRestrictivePolicy(),
	)
	if err != nil {
		return nil, fmt.Errorf("create go runtime: %w", err)
	}
	pyRT, err := docker.New(
		docker.WithDefaultClient(),
		docker.WithBaseImage("python"),
		docker.WithRestrictivePolicy(),
	)
	if err != nil {
		return nil, fmt.Errorf("create python runtime: %w", err)
	}
	compileCppJobExecutor := executors.NewCompileCppJobExecutor(log, inputProvider, outputProvider, gccRT)
	compileGoJobExecutor := executors.NewCompileGoJobExecutor(log, inputProvider, outputProvider, goRT)
	runCppJobExecutor := executors.NewRunCppJobExecutor(log, inputProvider, outputProvider, gccRT)
	runPyJobExecutor := executors.NewRunPyJobExecutor(log, inputProvider, outputProvider, pyRT)
	runGoJobExecutor := executors.NewRunGoJobExecutor(log, inputProvider, outputProvider, goRT)
	checkCppJobExecutor := executors.NewCheckCppJobExecutor(log, inputProvider, outputProvider, gccRT)
	return executor.NewJobExecutor(compileCppJobExecutor, compileGoJobExecutor, runCppJobExecutor, runPyJobExecutor, runGoJobExecutor, checkCppJobExecutor), nil
}
