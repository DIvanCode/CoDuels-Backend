package outbox

import (
	"time"
)

type Outbox struct {
	ID          int64      `json:"id"`
	Payload     string     `json:"payload"`
	CreatedAt   time.Time  `json:"created_at"`
	FailedAt    *time.Time `json:"failed_at"`
	FailedTries int        `json:"failed_tries"`
}
