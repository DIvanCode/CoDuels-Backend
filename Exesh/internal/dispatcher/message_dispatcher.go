package dispatcher

import (
	"context"
	"encoding/json"
	"exesh/internal/config"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/message/messages"
	"exesh/internal/domain/outbox"
	"fmt"
	"log/slog"
	"math"
	"strconv"
	"time"

	"github.com/segmentio/kafka-go"
	"github.com/segmentio/kafka-go/sasl/scram"
)

type (
	MessageDispatcher struct {
		log *slog.Logger

		kafkaEnabled bool

		unitOfWork     unitOfWork
		outboxStorage  outboxStorage
		messageStorage messageStorage

		writer *kafka.Writer
	}

	outboxStorage interface {
		CreateOutbox(ctx context.Context, ox outbox.Outbox) error
		GetOutboxForSend(ctx context.Context) (ox *outbox.Outbox, err error)
		SaveOutbox(ctx context.Context, ox outbox.Outbox) error
		DeleteOutbox(ctx context.Context, ox outbox.Outbox) error
	}

	messageStorage interface {
		CreateMessage(context.Context, execution.ID, string, time.Time) error
	}

	unitOfWork interface {
		Do(context.Context, func(context.Context) error) error
	}
)

func NewMessageDispatcher(
	log *slog.Logger,
	cfg config.DispatcherConfig,
	unitOfWork unitOfWork,
	outboxStorage outboxStorage,
	messageStorage messageStorage,
) *MessageDispatcher {
	writer := &kafka.Writer{
		Addr:        kafka.TCP(cfg.Brokers...),
		Topic:       cfg.Topic,
		MaxAttempts: 1,
		BatchSize:   1,
	}
	if cfg.SaslAuth {
		mechanism, err := scram.Mechanism(scram.SHA512, cfg.SaslUsername, cfg.SaslPassword)
		if err != nil {
			log.Error("failed to create kafka sasl mechanism", slog.Any("error", err))
		}

		writer.Transport = &kafka.Transport{
			SASL: mechanism,
		}
	}

	return &MessageDispatcher{
		log: log,

		kafkaEnabled: cfg.KafkaEnabled,

		unitOfWork:     unitOfWork,
		outboxStorage:  outboxStorage,
		messageStorage: messageStorage,

		writer: writer,
	}
}

func (s *MessageDispatcher) Start(ctx context.Context) {
	if !s.kafkaEnabled {
		return
	}
	go s.run(ctx)
}

func (s *MessageDispatcher) Send(ctx context.Context, msg messages.Message) error {
	payload, err := json.Marshal(msg)
	if err != nil {
		return fmt.Errorf("failed to marshal message: %w", err)
	}

	payloadStr := string(payload)
	createdAt := time.Now()

	if err = s.messageStorage.CreateMessage(ctx, msg.GetExecutionID(), payloadStr, createdAt); err != nil {
		return fmt.Errorf("failed to create message: %w", err)
	}

	if !s.kafkaEnabled {
		return nil
	}

	ox := outbox.Outbox{
		Payload:     payloadStr,
		CreatedAt:   createdAt,
		FailedAt:    nil,
		FailedTries: 0,
	}
	if err = s.outboxStorage.CreateOutbox(ctx, ox); err != nil {
		return fmt.Errorf("failed to create outbox: %w", err)
	}

	return nil
}

func (s *MessageDispatcher) run(ctx context.Context) {
	consequentFails := 0

	for {
		waitTime := time.Duration(10 * math.Pow(2, float64(min(consequentFails, 6))))
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

func (s *MessageDispatcher) process(ctx context.Context) error {
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
		if err = s.writer.WriteMessages(ctx, message); err != nil {
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
