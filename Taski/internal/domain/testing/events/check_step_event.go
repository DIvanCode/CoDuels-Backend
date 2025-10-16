package events

import "taski/internal/domain/testing"

type (
	CheckStepEvent struct {
		testing.EventDetails
		StepName    string      `json:"step_name"`
		CheckStatus CheckStatus `json:"status"`
	}

	CheckStatus string
)

const (
	CheckStatusOK CheckStatus = "OK"
	CheckStatusWA CheckStatus = "WA"
)
