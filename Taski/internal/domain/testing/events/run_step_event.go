package events

import "taski/internal/domain/testing"

type (
	RunStepEvent struct {
		testing.EventDetails
		StepName  string    `json:"step_name"`
		RunStatus RunStatus `json:"status"`
		Output    string    `json:"output,omitempty"`
	}

	RunStatus string
)

const (
	RunStatusOK RunStatus = "OK"
	RunStatusRE RunStatus = "RE"
	RunStatusTL RunStatus = "TL"
	RunStatusML RunStatus = "ML"
)
