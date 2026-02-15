package events

import (
	"taski/internal/domain/testing/event"
	"taski/internal/domain/testing/job"
)

type CheckJobEvent struct {
	event.Details
	JobName     job.Name   `json:"job"`
	CheckStatus job.Status `json:"status"`
}
