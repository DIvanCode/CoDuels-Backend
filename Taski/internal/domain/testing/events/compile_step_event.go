package events

import (
	"taski/internal/domain/testing"
)

type (
	CompileStepEvent struct {
		testing.EventDetails
		StepName      string        `json:"step_name"`
		CompileStatus CompileStatus `json:"status"`
		Error         string        `json:"error,omitempty"`
	}

	CompileStatus string
)

const (
	CompileStatusOK CompileStatus = "OK"
	CompileStatusCE CompileStatus = "CE"
)
