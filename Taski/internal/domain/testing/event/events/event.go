package events

import (
	"encoding/json"
	"fmt"
	"taski/internal/domain/testing/event"
)

type Event struct {
	event.IEvent
}

func (evt *Event) UnmarshalJSON(data []byte) error {
	var details event.Details
	if err := json.Unmarshal(data, &details); err != nil {
		return fmt.Errorf("failed to unmarshal event details: %w", err)
	}

	switch details.Type {
	case event.StartExecution:
		evt.IEvent = &StartExecutionEvent{}
	case event.CompileJob:
		evt.IEvent = &CompileJobEvent{}
	case event.RunJob:
		evt.IEvent = &RunJobEvent{}
	case event.CheckJob:
		evt.IEvent = &CheckJobEvent{}
	case event.FinishExecution:
		evt.IEvent = &FinishExecutionEvent{}
	default:
		return fmt.Errorf("unknown event type: %s", details.Type)
	}

	if err := json.Unmarshal(data, evt.IEvent); err != nil {
		return fmt.Errorf("failed to unmarshal %s event: %w", details.Type, err)
	}

	return nil
}

func (evt *Event) AsCompileJobEvent() *CompileJobEvent {
	return evt.IEvent.(*CompileJobEvent)
}

func (evt *Event) AsRunJobEvent() *RunJobEvent {
	return evt.IEvent.(*RunJobEvent)
}

func (evt *Event) AsCheckJobEvent() *CheckJobEvent {
	return evt.IEvent.(*CheckJobEvent)
}

func (evt *Event) AsFinishExecutionEvent() *FinishExecutionEvent {
	return evt.IEvent.(*FinishExecutionEvent)
}
