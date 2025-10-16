package consumer

import (
	"context"
	"fmt"
	"log/slog"
	"taski/internal/config"
	"taski/internal/domain/testing/events"
	"taski/internal/usecase/testing/usecase/update"
	"time"

	"github.com/segmentio/kafka-go"
)

type KafkaConsumer struct {
	log *slog.Logger
	cfg config.EventConsumerConfig

	reader  *kafka.Reader
	usecase *update.UseCase
}

func NewKafkaConsumer(log *slog.Logger, cfg config.EventConsumerConfig, usecase *update.UseCase) *KafkaConsumer {
	reader := kafka.NewReader(kafka.ReaderConfig{
		Brokers: cfg.Brokers,
		Topic:   cfg.Topic,
		GroupID: cfg.GroupID,
	})

	return &KafkaConsumer{
		log: log,
		cfg: cfg,

		reader:  reader,
		usecase: usecase,
	}
}

func (c *KafkaConsumer) Start(ctx context.Context) {
	go c.runConsumer(ctx)
}

func (c *KafkaConsumer) runConsumer(ctx context.Context) {
	c.log.Info("Kafka consumer started", slog.String("topic", c.reader.Config().Topic))

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

func (c *KafkaConsumer) processMessage(ctx context.Context, msg kafka.Message) error {
	c.log.Info("Received message", slog.Int64("offset", msg.Offset), slog.String("key", string(msg.Key)))

	event, err := events.UnmarshalEventJSON(msg.Value)
	if err != nil {
		return fmt.Errorf("failed to unmarshal event: %w", err)
	}

	command := update.Command{Event: event}
	if err := c.usecase.Update(ctx, command); err != nil {
		return fmt.Errorf("failed to process event: %w", err)
	}

	if err := c.reader.CommitMessages(ctx, msg); err != nil {
		return fmt.Errorf("failed to commit message: %w", err)
	}

	c.log.Info("Successfully processed event", slog.Any("type", event.GetType()), slog.Any("execution_id", event.GetExecutionID()))
	return nil
}

func (c *KafkaConsumer) Close() error {
	if c.reader != nil {
		return c.reader.Close()
	}
	return nil
}
