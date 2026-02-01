package messages

import (
	"encoding/json"
	"exesh/internal/domain/execution/message"
	"fmt"
)

type Message struct {
	message.IMessage
}

func (msg *Message) UnmarshalJSON(data []byte) error {
	var details message.Details
	if err := json.Unmarshal(data, &details); err != nil {
		return fmt.Errorf("failed to unmarshal message details: %w", err)
	}

	switch details.Type {
	case message.StartExecution:
		msg.IMessage = &StartExecutionMessage{}
	case message.CompileJob:
		msg.IMessage = &CompileJobMessage{}
	case message.RunJob:
		msg.IMessage = &RunJobMessage{}
	case message.CheckJob:
		msg.IMessage = &CheckJobMessage{}
	case message.FinishExecution:
		msg.IMessage = &FinishExecutionMessage{}
	default:
		return fmt.Errorf("unknown output type: %s", details.Type)
	}

	if err := json.Unmarshal(data, msg.IMessage); err != nil {
		return fmt.Errorf("failed to unmarshal %s message: %w", details.Type, err)
	}

	return nil
}
