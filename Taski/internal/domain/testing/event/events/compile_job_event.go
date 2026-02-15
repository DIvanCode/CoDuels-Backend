package events

import (
	"taski/internal/domain/testing/event"
	"taski/internal/domain/testing/job"
)

type CompileJobEvent struct {
	event.Details
	JobName          job.Name   `json:"job"`
	CompileStatus    job.Status `json:"status"`
	CompilationError *string    `json:"compilation_error,omitempty"`
}
