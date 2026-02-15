package events

import (
	"taski/internal/domain/testing/event"
	"taski/internal/domain/testing/job"
)

type RunJobEvent struct {
	event.Details
	JobName   job.Name   `json:"job"`
	RunStatus job.Status `json:"status"`
	Output    *string    `json:"output,omitempty"`
}
