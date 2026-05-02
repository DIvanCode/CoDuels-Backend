package main

import (
	"context"
	"errors"
	"exesh/internal/config"
	"exesh/internal/domain/execution/job"
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

	promRegistry := prometheus.NewRegistry()
	promRegistry.MustRegister(
		collectors.NewGoCollector(),
		collectors.NewProcessCollector(collectors.ProcessCollectorOpts{}),
	)
	executorFactory := setupExecutorFactory(log, sourceProvider, outputProvider)
	w := worker.NewWorker(log, cfg.Worker, sourceProvider, executorFactory)
	if err = w.RegisterMetrics(promRegistry); err != nil {
		log.Error("could not register worker metrics", slog.Any("err", err))
		return
	}
	w.Start(ctx)

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

func setupExecutorFactory(
	log *slog.Logger,
	sourceProvider *provider.SourceProvider,
	outputProvider *provider.OutputProvider,
) *executor.ExecutorFactory {
	localRuntimeFactory := local.NewRuntimeFactory(job.CompileCpp, job.CompileGo)
	isolateRuntimeFactory := isolate.NewRuntimeFactory(job.RunCpp, job.RunPy, job.RunGo, job.CheckCpp)
	runtimeFactory := runtime.NewJobRuntimeFactory(localRuntimeFactory, isolateRuntimeFactory)

	compileCppExecutorFactory := executors.NewCompileCppExecutorFactory(log, sourceProvider, outputProvider, localRuntimeFactory)
	compileGoExecutorFactory := executors.NewCompileGoExecutorFactory(log, sourceProvider, outputProvider, localRuntimeFactory)
	runCppExecutorFactory := executors.NewRunCppExecutorFactory(log, sourceProvider, outputProvider, isolateRuntimeFactory)
	runPyExecutorFactory := executors.NewRunPyExecutorFactory(log, sourceProvider, outputProvider, isolateRuntimeFactory)
	runGoExecutorFactory := executors.NewRunGoExecutorFactory(log, sourceProvider, outputProvider, isolateRuntimeFactory)
	checkCppExecutorFactory := executors.NewCheckCppExecutorFactory(log, sourceProvider, outputProvider, isolateRuntimeFactory)

	baseExecutorFactory := executor.NewExecutorFactory(
		compileCppExecutorFactory,
		compileGoExecutorFactory,
		runCppExecutorFactory,
		runPyExecutorFactory,
		runGoExecutorFactory,
		checkCppExecutorFactory,
	)
	chainExecutorFactory := executors.NewChainExecutorFactory(log, sourceProvider, runtimeFactory, baseExecutorFactory)

	executorFactory := executor.NewExecutorFactory(
		compileCppExecutorFactory,
		compileGoExecutorFactory,
		runCppExecutorFactory,
		runPyExecutorFactory,
		runGoExecutorFactory,
		checkCppExecutorFactory,
		chainExecutorFactory,
	)

	return executorFactory
}
