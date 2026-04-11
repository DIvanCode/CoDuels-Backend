package history

import "exesh/internal/domain/execution/message/messages"

type Message struct {
	MessageID int64            `json:"message_id"`
	Message   messages.Message `json:"message"`
}
