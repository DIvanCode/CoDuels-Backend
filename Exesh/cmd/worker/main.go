package main

import (
	"context"
	"exesh/internal/config"
	"exesh/internal/executor"
	"exesh/internal/executor/executors"
	"exesh/internal/provider"
	"exesh/internal/provider/providers"
	"exesh/internal/provider/providers/adapter"
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

	jobExecutor := setupJobExecutor(log, inputProvider, outputProvider)

	worker.NewWorker(log, cfg.Worker, jobExecutor).Start(ctx)

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
	case "docker":
		log = slog.New(slog.NewTextHandler(os.Stdout, &slog.HandlerOptions{Level: slog.LevelDebug}))
	default:
		err = fmt.Errorf("failed setup logger for env %s", env)
	}

	return
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

func setupJobExecutor(log *slog.Logger, inputProvider *provider.InputProvider, outputProvider *provider.OutputProvider) *executor.JobExecutor {
	compileCppJobExecutor := executors.NewCompileCppJobExecutor(log, inputProvider, outputProvider)
	runCppJobExecutor := executors.NewRunCppJobExecutor(log, inputProvider, outputProvider)
	runPyJobExecutor := executors.NewRunPyJobExecutor(log, inputProvider, outputProvider)
	runGoJobExecutor := executors.NewRunGoJobExecutor(log, inputProvider, outputProvider)
	checkCppJobExecutor := executors.NewCheckCppJobExecutor(log, inputProvider, outputProvider)
	return executor.NewJobExecutor(compileCppJobExecutor, runCppJobExecutor, runPyJobExecutor, runGoJobExecutor, checkCppJobExecutor)
}
