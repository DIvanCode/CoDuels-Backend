package events

import "taski/internal/domain/testing/event"

type FinishExecutionEvent struct {
	event.Details
	Error *string `json:"error,omitempty"`
}
