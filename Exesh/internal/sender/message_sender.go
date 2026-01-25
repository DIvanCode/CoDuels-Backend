package sender

import (
	"context"
	"encoding/json"
	"exesh/internal/config"
	"exesh/internal/domain/execution"
	"exesh/internal/lib/queue"
	"log/slog"
	"math"
	"sync"
	"time"

	"github.com/segmentio/kafka-go"
)

type KafkaSender struct {
	log *slog.Logger

	messages queue.Queue[execution.Message]
	mu       sync.Mutex

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
		log: log,

		messages: *queue.NewQueue[execution.Message](),
		mu:       sync.Mutex{},

		writer: writer,
	}
}

func (s *KafkaSender) Start(ctx context.Context) {
	go s.run(ctx)
}

func (s *KafkaSender) Send(msg execution.Message) {
	s.mu.Lock()
	defer s.mu.Unlock()

	s.messages.Enqueue(msg)
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

		ok := true
		for {
			s.mu.Lock()
			msg := s.messages.Peek()
			s.mu.Unlock()

			if msg == nil {
				break
			}

			value, err := json.Marshal(*msg)
			if err != nil {
				s.mu.Lock()
				s.messages.Dequeue()
				s.mu.Unlock()
				continue
			}

			kafkaMsg := kafka.Message{
				Key:   []byte((*msg).GetExecutionID().String()),
				Value: value,
			}

			s.log.Debug("send to kafka", slog.Any("type", (*msg).GetType()))
			if err = s.writer.WriteMessages(ctx, kafkaMsg); err != nil {
				s.log.Error("failed to send message to kafka", slog.Any("error", err))
				ok = false
				break
			}

			s.mu.Lock()
			s.messages.Dequeue()
			s.mu.Unlock()
		}

		if ok {
			consequentFails = 0
		} else {
			consequentFails++
		}
	}
}
