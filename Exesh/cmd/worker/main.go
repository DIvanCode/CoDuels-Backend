package main

import (
	"context"
	"errors"
	"exesh/internal/config"
	"exesh/internal/executor"
	"exesh/internal/executor/executors"
	"exesh/internal/provider"
	"exesh/internal/provider/adapter"
	"exesh/internal/runtime"
	"exesh/internal/runtime/isolate"
	"exesh/internal/runtime/local"
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

	"github.com/prometheus/client_golang/prometheus"
	"github.com/prometheus/client_golang/prometheus/collectors"
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

	fs, err := filestorage.New(log, cfg.FileStorage.ToExternal(), mux)
	if err != nil {
		log.Error("failed to create filestorage", slog.String("error", err.Error()))
		return
	}
	defer fs.Shutdown()

	filestorageAdapter := adapter.NewFilestorageAdapter(fs)
	sourceProvider := provider.NewSourceProvider(cfg.SourceProvider, filestorageAdapter)
	outputProvider := provider.NewOutputProvider(cfg.OutputProvider, filestorageAdapter)

	executorFactory, err := setupExecutorFactory(log, sourceProvider, outputProvider, cfg.Runtime)
	if err != nil {
		flog.Fatal(err)
	}

	worker.NewWorker(log, cfg.Worker, sourceProvider, executorFactory).Start(ctx)

	promRegistry := prometheus.NewRegistry()
	promRegistry.MustRegister(
		collectors.NewGoCollector(),
		collectors.NewProcessCollector(collectors.ProcessCollectorOpts{}),
	)

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

	return log, err
}

func setupExecutorFactory(
	log *slog.Logger,
	sourceProvider *provider.SourceProvider,
	outputProvider *provider.OutputProvider,
	runtimeName string,
) (*executor.ExecutorFactory, error) {
	var (
		compileCppRuntime runtime.Runtime
		compileGoRuntime  runtime.Runtime
		runCppRuntime     runtime.Runtime
		runGoRuntime      runtime.Runtime
		runPyRuntime      runtime.Runtime
	)

	localRuntime := local.New()
	compileCppRuntime = localRuntime
	compileGoRuntime = localRuntime

	switch runtimeName {
	case "local":
		runCppRuntime = localRuntime
		runGoRuntime = localRuntime
		runPyRuntime = localRuntime
	case "isolate":
		isolateRT := isolate.New()
		runCppRuntime = isolateRT
		runGoRuntime = isolateRT
		runPyRuntime = isolateRT
	default:
		return nil, fmt.Errorf("unknown runtime name: %s", runtimeName)
	}

	return executor.NewExecutorFactory(
		executors.NewCompileCppExecutorFactory(log, sourceProvider, outputProvider, compileCppRuntime),
		executors.NewCompileGoExecutorFactory(log, sourceProvider, outputProvider, compileGoRuntime),
		executors.NewRunCppExecutorFactory(log, sourceProvider, outputProvider, runCppRuntime),
		executors.NewRunPyExecutorFactory(log, sourceProvider, outputProvider, runPyRuntime),
		executors.NewRunGoExecutorFactory(log, sourceProvider, outputProvider, runGoRuntime),
		executors.NewCheckCppExecutorFactory(log, sourceProvider, outputProvider, runCppRuntime),
	), nil
}
