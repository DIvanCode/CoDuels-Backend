package sender

import (
	"context"
	"exesh/internal/domain/execution"
	"fmt"
	"log/slog"
)

type MessageSender struct {
	log *slog.Logger
}

func NewMessageSender(log *slog.Logger) *MessageSender {
	return &MessageSender{
		log: log,
	}
}

func (s *MessageSender) Send(ctx context.Context, msg execution.Message) error {
	return fmt.Errorf("not implemented")
}
