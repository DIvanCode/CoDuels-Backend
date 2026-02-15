package producer

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"math"
	"strconv"
	"taski/internal/config"
	"taski/internal/domain/outbox"
	"taski/internal/domain/testing/message/messages"
	"time"

	"github.com/segmentio/kafka-go"
)

type (
	MessageProducer struct {
		log *slog.Logger

		unitOfWork    unitOfWork
		outboxStorage outboxStorage

		writer *kafka.Writer
	}

	outboxStorage interface {
		CreateOutbox(ctx context.Context, ox outbox.Outbox) error
		GetOutboxForProduce(ctx context.Context) (ox *outbox.Outbox, err error)
		SaveOutbox(ctx context.Context, ox outbox.Outbox) error
		DeleteOutbox(ctx context.Context, ox outbox.Outbox) error
	}

	unitOfWork interface {
		Do(context.Context, func(context.Context) error) error
	}
)

func NewMessageProducer(
	log *slog.Logger,
	cfg config.MessageProducerConfig,
	unitOfWork unitOfWork,
	outboxStorage outboxStorage,
) *MessageProducer {
	writer := &kafka.Writer{
		Addr:        kafka.TCP(cfg.Brokers...),
		Topic:       cfg.Topic,
		MaxAttempts: 1,
		BatchSize:   1,
	}

	return &MessageProducer{
		log: log,

		unitOfWork:    unitOfWork,
		outboxStorage: outboxStorage,

		writer: writer,
	}
}

func (p *MessageProducer) Start(ctx context.Context) {
	go p.run(ctx)
}

func (p *MessageProducer) Produce(ctx context.Context, msg messages.Message) error {
	payload, err := json.Marshal(msg)
	if err != nil {
		return fmt.Errorf("failed to marshal message: %w", err)
	}

	ox := outbox.Outbox{
		Payload:     string(payload),
		CreatedAt:   time.Now(),
		FailedAt:    nil,
		FailedTries: 0,
	}
	if err = p.outboxStorage.CreateOutbox(ctx, ox); err != nil {
		return fmt.Errorf("failed to create outbox: %w", err)
	}

	return nil
}

func (p *MessageProducer) run(ctx context.Context) {
	consequentFails := 0

	for {
		waitTime := time.Duration(100 * math.Pow(2, float64(min(consequentFails, 6))))
		timer := time.NewTicker(waitTime * time.Millisecond)

		select {
		case <-ctx.Done():
			return
		case <-timer.C:
			break
		}

		if err := p.process(ctx); err != nil {
			p.log.Error("failed to process outbox", slog.Any("error", err))
			consequentFails++
			continue
		}

		consequentFails = 0
	}
}

func (p *MessageProducer) process(ctx context.Context) error {
	uowCtx, cancel := context.WithTimeout(ctx, 10*time.Second)
	defer cancel()

	if err := p.unitOfWork.Do(uowCtx, func(ctx context.Context) error {
		ox, err := p.outboxStorage.GetOutboxForProduce(ctx)
		if err != nil {
			return fmt.Errorf("failed to get outbox for send: %w", err)
		}

		if ox == nil {
			return nil
		}

		if ox.FailedTries != 0 {
			retryTimeout := time.Duration(100 * math.Pow(2, float64(min(ox.FailedTries, 6))))
			if ox.FailedAt.Add(retryTimeout * time.Millisecond).Before(time.Now()) {
				return nil
			}
		}

		message := kafka.Message{
			Key:   []byte(strconv.FormatInt(ox.ID, 10)),
			Value: []byte(ox.Payload),
		}

		p.log.Debug("produce to kafka", slog.Int64("outbox_id", ox.ID))
		err = p.writer.WriteMessages(ctx, message)
		if err != nil {
			failedAt := time.Now()
			ox.FailedAt = &failedAt
			ox.FailedTries++

			_ = p.outboxStorage.SaveOutbox(ctx, *ox)
			return fmt.Errorf("failed to produce message to kafka: %w", err)
		}

		if err = p.outboxStorage.DeleteOutbox(ctx, *ox); err != nil {
			return fmt.Errorf("failed to delete outbox: %w", err)
		}

		return nil
	}); err != nil {
		return err
	}

	return nil
}
