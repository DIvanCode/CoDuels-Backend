package handler

import (
	"context"
	"fmt"
	"log/slog"
	"taski/internal/config"
	"taski/internal/consumer"
	"taski/internal/domain/testing"
	"taski/internal/usecase/testing/usecase/update"
)

type (
	unitOfWork interface {
		Do(context.Context, func(ctx context.Context) error) error
	}

	solutionStorage interface {
		GetInProgress(context.Context) ([]testing.Solution, error)
	}

	eventProcessor interface {
		Start(context.Context)
		Close() error
	}
)

type EventHandler struct {
	log       *slog.Logger
	cfg       *config.Config
	processor eventProcessor
}

func NewEventHandler(
	log *slog.Logger,
	cfg *config.Config,
	unitOfWork unitOfWork,
	solutionStorage solutionStorage,
	usecase *update.UseCase,
) *EventHandler {
	mode := cfg.EventConsumer.Mode
	if mode == "" {
		mode = "kafka"
	}

	var processor eventProcessor
	eventConsumerCfg := cfg.EventConsumer
	if eventConsumerCfg.RestEndpoint == "" {
		eventConsumerCfg.RestEndpoint = cfg.Execute.Endpoint
	}
	switch mode {
	case "rest":
		processor = consumer.NewEventPoller(log, eventConsumerCfg, unitOfWork, solutionStorage, usecase)
	default:
		processor = consumer.NewEventConsumer(log, eventConsumerCfg, usecase)
	}

	return &EventHandler{
		log:       log,
		cfg:       cfg,
		processor: processor,
	}
}

func (h *EventHandler) Start(ctx context.Context) {
	h.log.Info("event handler started", slog.String("mode", h.mode()))
	h.processor.Start(ctx)
}

func (h *EventHandler) Close() error {
	if h.processor == nil {
		return nil
	}
	if err := h.processor.Close(); err != nil {
		return fmt.Errorf("failed to close event handler processor: %w", err)
	}
	return nil
}

func (h *EventHandler) mode() string {
	if h.cfg.EventConsumer.Mode == "" {
		return "kafka"
	}
	return h.cfg.EventConsumer.Mode
}
