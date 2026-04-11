package history

import "taski/internal/domain/testing/message/messages"

type Message struct {
	MessageID int64            `json:"message_id"`
	Message   messages.Message `json:"message"`
}
