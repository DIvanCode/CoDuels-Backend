package messages

import (
	"encoding/json"
	"fmt"
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

func (msg *Message) UnmarshalJSON(data []byte) error {
	var details message.Details
	if err := json.Unmarshal(data, &details); err != nil {
		return fmt.Errorf("failed to unmarshal message details: %w", err)
	}

	switch details.Type {
	case message.StartTestingMessage:
		msg.IMessage = &StartTestingMessage{}
	case message.UpdateStatusMessage:
		msg.IMessage = &UpdateStatusMessage{}
	case message.FinishTestingMessage:
		msg.IMessage = &FinishTestingMessage{}
	default:
		return fmt.Errorf("unknown output type: %s", details.Type)
	}

	if err := json.Unmarshal(data, msg.IMessage); err != nil {
		return fmt.Errorf("failed to unmarshal %s message: %w", details.Type, err)
	}

	return nil
}
