package producer

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"taski/internal/config"
	"taski/internal/domain/testing"

	"github.com/segmentio/kafka-go"
)

type KafkaProducer struct {
	log    *slog.Logger
	writer *kafka.Writer
}

func NewKafkaProducer(log *slog.Logger, cfg config.MessageProducerConfig) *KafkaProducer {
	writer := &kafka.Writer{
		Addr:        kafka.TCP(cfg.Brokers...),
		Topic:       cfg.Topic,
		MaxAttempts: 1,
		BatchSize:   1,
	}
	return &KafkaProducer{
		log:    log,
		writer: writer,
	}
}

func (p *KafkaProducer) Produce(ctx context.Context, msg testing.Message) error {
	value, err := json.Marshal(msg)
	if err != nil {
		return fmt.Errorf("failed to marshal message: %w", err)
	}

	kafkaMsg := kafka.Message{
		Key:   []byte(msg.GetSolutionID()),
		Value: value,
	}

	p.log.Info("sending message to kafka", slog.Any("msg_type", msg.GetType()))
	if err = p.writer.WriteMessages(ctx, kafkaMsg); err != nil {
		return fmt.Errorf("failed to send message to kafka: %w", err)
	}
	p.log.Info("sending ok")

	return nil
}
