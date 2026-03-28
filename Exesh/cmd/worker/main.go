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
	"exesh/internal/runtime/docker"
	isolatert "exesh/internal/runtime/isolate"
	localrt "exesh/internal/runtime/local"
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

	jobExecutor, err := setupJobExecutor(log, sourceProvider, outputProvider, cfg.Runtime)
	if err != nil {
		flog.Fatal(err)
	}

	worker.NewWorker(log, cfg.Worker, sourceProvider, jobExecutor).Start(ctx)

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

func setupJobExecutor(log *slog.Logger, sourceProvider *provider.SourceProvider, outputProvider *provider.OutputProvider, runtimeName string) (*executor.JobExecutor, error) {
	var (
		compileCppFactory func() (runtime.Runtime, error)
		compileGoFactory  func() (runtime.Runtime, error)
		runCppFactory     func() (runtime.Runtime, error)
		runGoFactory      func() (runtime.Runtime, error)
		runPyFactory      func() (runtime.Runtime, error)
		chainFactory      func() (runtime.Runtime, error)
	)

	compileCppFactory = func() (runtime.Runtime, error) { return localrt.New(), nil }
	compileGoFactory = func() (runtime.Runtime, error) { return localrt.New(), nil }

	switch runtimeName {
	case "docker":
		runCppFactory = func() (runtime.Runtime, error) {
			return docker.New(docker.WithDefaultClient(), docker.WithBaseImage("gcc:latest"), docker.WithRestrictivePolicy())
		}
		runGoFactory = func() (runtime.Runtime, error) {
			return docker.New(docker.WithDefaultClient(), docker.WithBaseImage("golang:latest"), docker.WithRestrictivePolicy())
		}
		runPyFactory = func() (runtime.Runtime, error) {
			return docker.New(docker.WithDefaultClient(), docker.WithBaseImage("python:3"), docker.WithRestrictivePolicy())
		}
		chainFactory = func() (runtime.Runtime, error) {
			return docker.New(docker.WithDefaultClient(), docker.WithBaseImage("python:3"), docker.WithRestrictivePolicy())
		}
	case "local":
		runCppFactory = func() (runtime.Runtime, error) { return localrt.New(), nil }
		runGoFactory = func() (runtime.Runtime, error) { return localrt.New(), nil }
		runPyFactory = func() (runtime.Runtime, error) { return localrt.New(), nil }
		chainFactory = func() (runtime.Runtime, error) { return localrt.New(), nil }
	case "isolate":
		runCppFactory = func() (runtime.Runtime, error) { return isolatert.New(), nil }
		runGoFactory = func() (runtime.Runtime, error) { return isolatert.New(), nil }
		runPyFactory = func() (runtime.Runtime, error) { return isolatert.New(), nil }
		chainFactory = func() (runtime.Runtime, error) { return isolatert.New(), nil }
	default:
		return nil, fmt.Errorf("unknown runtime name: %s", runtimeName)
	}

	compileCppJobExecutor := executors.NewCompileCppJobExecutor(log, sourceProvider, outputProvider, compileCppFactory)
	compileGoJobExecutor := executors.NewCompileGoJobExecutor(log, sourceProvider, outputProvider, compileGoFactory)
	runCppJobExecutor := executors.NewRunCppJobExecutor(log, sourceProvider, outputProvider, runCppFactory)
	runPyJobExecutor := executors.NewRunPyJobExecutor(log, sourceProvider, outputProvider, runPyFactory)
	runGoJobExecutor := executors.NewRunGoJobExecutor(log, sourceProvider, outputProvider, runGoFactory)
	checkCppJobExecutor := executors.NewCheckCppJobExecutor(log, sourceProvider, outputProvider, runCppFactory)
	chainJobExecutor := executors.NewChainJobExecutor(
		log,
		chainFactory,
		compileCppJobExecutor,
		compileGoJobExecutor,
		runCppJobExecutor,
		runPyJobExecutor,
		runGoJobExecutor,
		checkCppJobExecutor,
	)
	return executor.NewJobExecutor(
		compileCppJobExecutor,
		compileGoJobExecutor,
		runCppJobExecutor,
		runPyJobExecutor,
		runGoJobExecutor,
		checkCppJobExecutor,
		chainJobExecutor,
	), nil
}
