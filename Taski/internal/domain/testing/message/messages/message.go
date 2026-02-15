package messages

import (
	"encoding/json"
	"taski/internal/domain/testing/message"
)

type Message struct {
	message.IMessage
}

func (msg Message) MarshalJSON() ([]byte, error) {
	if msg.IMessage == nil {
		return []byte("null"), nil
	}

	return json.Marshal(msg.IMessage)
}
