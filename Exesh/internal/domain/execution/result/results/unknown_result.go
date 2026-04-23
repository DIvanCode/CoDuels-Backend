package results

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/result"
	"time"
)

type UnknownResult struct {
	result.Details
}

func NewUnknownResultErr(jobID job.ID, err string) Result {
	return Result{
		&UnknownResult{
			Details: result.Details{
				Type:      result.Unknown,
				JobID:     jobID,
				HasOutput: false,
				DoneAt:    time.Now(),
				Error:     err,
			},
		},
	}
}
