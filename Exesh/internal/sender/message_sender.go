package sender

import (
	"context"
	"encoding/json"
	"exesh/internal/config"
	"exesh/internal/domain/execution"
	"fmt"
	"log/slog"

	"github.com/segmentio/kafka-go"
)

type KafkaSender struct {
	log    *slog.Logger
	writer *kafka.Writer
}

func NewKafkaSender(log *slog.Logger, cfg config.SenderConfig) *KafkaSender {
	writer := &kafka.Writer{
		Addr:        kafka.TCP(cfg.Brokers...),
		Topic:       cfg.Topic,
		MaxAttempts: 1,
		BatchSize:   1,
	}
	return &KafkaSender{
		log:    log,
		writer: writer,
	}
}

func (s *KafkaSender) Send(ctx context.Context, msg execution.Message) error {
	value, err := json.Marshal(msg)
	if err != nil {
		return fmt.Errorf("failed to marshal message: %w", err)
	}

	kafkaMsg := kafka.Message{
		Key:   []byte(msg.GetExecutionID().String()),
		Value: value,
	}

	s.log.Info("sending message to kafka", slog.Any("msg_type", msg.GetType()))
	if err = s.writer.WriteMessages(ctx, kafkaMsg); err != nil {
		return fmt.Errorf("failed to send message to kafka: %w", err)
	}
	s.log.Info("sending ok")

	return nil
}
