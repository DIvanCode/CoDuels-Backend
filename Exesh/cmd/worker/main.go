package main

import (
	"context"
	"exesh/internal/config"
	"exesh/internal/executor"
	"exesh/internal/executor/executors"
	"exesh/internal/provider"
	"exesh/internal/provider/providers"
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

	inputProvider := setupInputProvider(cfg.InputProvider, filestorage)
	outputProvider := setupOutputProvider(cfg.OutputProvider, filestorage)

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

	return log, err
}

func setupInputProvider(cfg config.InputProviderConfig, filestorage filestorage.FileStorage) *provider.InputProvider {
	filestorageBucketInputProvider := providers.NewFilestorageBucketInputProvider(filestorage, cfg.FilestorageBucketTTL)
	artifactInputProvider := providers.NewArtifactInputProvider(filestorage, cfg.ArtifactTTL)
	return provider.NewInputProvider(filestorageBucketInputProvider, artifactInputProvider)
}

func setupOutputProvider(cfg config.OutputProviderConfig, filestorage filestorage.FileStorage) *provider.OutputProvider {
	artifactOutputProvider := providers.NewArtifactOutputProvider(filestorage, cfg.ArtifactTTL)
	return provider.NewOutputProvider(artifactOutputProvider)
}

func setupJobExecutor(log *slog.Logger, inputProvider *provider.InputProvider, outputProvider *provider.OutputProvider) (*executor.JobExecutor, error) {
	rt, err := docker.New(
		docker.WithDefaultClient(),
		docker.WithBaseImage("gcc"),
		docker.WithRestrictivePolicy(),
	)
	if err != nil {
		return nil, fmt.Errorf("create cpp runtime: %w", err)
	}
	compileCppJobExecutor := executors.NewCompileCppJobExecutor(log, inputProvider, outputProvider, rt)
	compileGoJobExecutor := executors.NewCompileGoJobExecutor(log, inputProvider, outputProvider, rt)
	runCppJobExecutor := executors.NewRunCppJobExecutor(log, inputProvider, outputProvider, rt)
	runPyJobExecutor := executors.NewRunPyJobExecutor(log, inputProvider, outputProvider, rt)
	runGoJobExecutor := executors.NewRunGoJobExecutor(log, inputProvider, outputProvider, rt)
	checkCppJobExecutor := executors.NewCheckCppJobExecutor(log, inputProvider, outputProvider, rt)
	return executor.NewJobExecutor(compileCppJobExecutor, compileGoJobExecutor, runCppJobExecutor, runPyJobExecutor, runGoJobExecutor, checkCppJobExecutor), nil
}
