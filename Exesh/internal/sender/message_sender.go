package sender

import (
	"context"
	"encoding/json"
	"exesh/internal/config"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/outbox"
	"fmt"
	"log/slog"
	"math"
	"strconv"
	"time"

	"github.com/segmentio/kafka-go"
)

type (
	KafkaSender struct {
		log *slog.Logger

		unitOfWork    unitOfWork
		outboxStorage outboxStorage

		writer *kafka.Writer
	}

	outboxStorage interface {
		CreateOutbox(ctx context.Context, ox outbox.Outbox) error
		GetOutboxForSend(ctx context.Context) (ox *outbox.Outbox, err error)
		SaveOutbox(ctx context.Context, ox outbox.Outbox) error
		DeleteOutbox(ctx context.Context, ox outbox.Outbox) error
	}

	unitOfWork interface {
		Do(context.Context, func(context.Context) error) error
	}
)

func NewKafkaSender(
	log *slog.Logger,
	cfg config.SenderConfig,
	unitOfWork unitOfWork,
	outboxStorage outboxStorage,
) *KafkaSender {
	writer := &kafka.Writer{
		Addr:        kafka.TCP(cfg.Brokers...),
		Topic:       cfg.Topic,
		MaxAttempts: 1,
		BatchSize:   1,
	}

	return &KafkaSender{
		log: log,

		unitOfWork:    unitOfWork,
		outboxStorage: outboxStorage,

		writer: writer,
	}
}

func (s *KafkaSender) Start(ctx context.Context) {
	go s.run(ctx)
}

func (s *KafkaSender) Send(ctx context.Context, msg execution.Message) error {
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
	if err = s.outboxStorage.CreateOutbox(ctx, ox); err != nil {
		return fmt.Errorf("failed to create outbox: %w", err)
	}

	return nil
}

func (s *KafkaSender) run(ctx context.Context) {
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

		if err := s.process(ctx); err != nil {
			s.log.Error("failed to process outbox", slog.Any("error", err))
			consequentFails++
			continue
		}

		consequentFails = 0
	}
}

func (s *KafkaSender) process(ctx context.Context) error {
	uowCtx, cancel := context.WithTimeout(ctx, 10*time.Second)
	defer cancel()

	if err := s.unitOfWork.Do(uowCtx, func(ctx context.Context) error {
		ox, err := s.outboxStorage.GetOutboxForSend(ctx)
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

		s.log.Debug("send to kafka", slog.Int64("outbox_id", ox.ID))
		err = s.writer.WriteMessages(ctx, message)
		if err != nil {
			failedAt := time.Now()
			ox.FailedAt = &failedAt
			ox.FailedTries++

			_ = s.outboxStorage.SaveOutbox(ctx, *ox)
			return fmt.Errorf("failed to send message to kafka: %w", err)
		}

		if err = s.outboxStorage.DeleteOutbox(ctx, *ox); err != nil {
			return fmt.Errorf("failed to delete outbox: %w", err)
		}

		return nil
	}); err != nil {
		return err
	}

	return nil
}
