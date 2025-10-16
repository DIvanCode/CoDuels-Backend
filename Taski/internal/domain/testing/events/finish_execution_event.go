package events

import "taski/internal/domain/testing"

type FinishExecutionEvent struct {
	testing.EventDetails
	Error string `json:"error,omitempty"`
}
