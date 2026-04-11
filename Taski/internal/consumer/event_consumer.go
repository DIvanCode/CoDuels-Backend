package consumer

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"taski/internal/config"
	"taski/internal/domain/testing/event/events"
	"taski/internal/usecase/testing/usecase/update"
	"time"

	"github.com/segmentio/kafka-go"
	"github.com/segmentio/kafka-go/sasl/scram"
)

type EventConsumer struct {
	log *slog.Logger
	cfg config.EventConsumerConfig

	reader  *kafka.Reader
	usecase *update.UseCase
}

func NewEventConsumer(log *slog.Logger, cfg config.EventConsumerConfig, usecase *update.UseCase) *EventConsumer {
	readerConfig := kafka.ReaderConfig{
		Brokers: cfg.Brokers,
		Topic:   cfg.Topic,
		GroupID: cfg.GroupID,
	}

	if cfg.SaslAuth {
		mechanism, err := scram.Mechanism(scram.SHA512, cfg.SaslUsername, cfg.SaslPassword)
		if err != nil {
			log.Error("failed to create kafka sasl mechanism", slog.Any("error", err))
		}

		readerConfig.Dialer = &kafka.Dialer{
			SASLMechanism: mechanism,
		}
	}

	reader := kafka.NewReader(readerConfig)

	return &EventConsumer{
		log: log,
		cfg: cfg,

		reader:  reader,
		usecase: usecase,
	}
}

func (c *EventConsumer) Start(ctx context.Context) {
	go c.runConsumer(ctx)
}

func (c *EventConsumer) runConsumer(ctx context.Context) {
	c.log.Info("kafka consumer started", slog.String("topic", c.reader.Config().Topic))

	for {
		timer := time.NewTicker(c.cfg.FetchInterval)

		select {
		case <-timer.C:
			break
		case <-ctx.Done():
			return
		}

		msg, err := c.reader.FetchMessage(ctx)
		if err != nil {
			slog.Error("failed to fetch message", slog.Any("error", err))
			continue
		}

		if err := c.processMessage(ctx, msg); err != nil {
			slog.Error("failed to process message", slog.Any("error", err))
			continue
		}
	}
}

func (c *EventConsumer) processMessage(ctx context.Context, msg kafka.Message) error {
	c.log.Info("received message", slog.Int64("offset", msg.Offset), slog.String("key", string(msg.Key)))

	var evt events.Event
	if err := json.Unmarshal(msg.Value, &evt); err != nil {
		return fmt.Errorf("failed to unmarshal event: %w", err)
	}

	command := update.Command{Event: evt}
	if err := c.usecase.Update(ctx, command); err != nil {
		return fmt.Errorf("failed to process event: %w", err)
	}

	if err := c.reader.CommitMessages(ctx, msg); err != nil {
		return fmt.Errorf("failed to commit message: %w", err)
	}

	c.log.Info("successfully processed event", slog.Any("type", evt.GetType()), slog.Any("execution_id", evt.GetExecutionID()))
	return nil
}

func (c *EventConsumer) Close() error {
	if c.reader != nil {
		return c.reader.Close()
	}
	return nil
}
