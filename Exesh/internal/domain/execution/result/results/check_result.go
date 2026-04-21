package results

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/result"
	"time"
)

type CheckResult struct {
	result.Details
}

func NewCheckResultOK(jobID job.ID, elapsedTime int, usedMemory int) Result {
	return Result{
		&CheckResult{
			Details: result.Details{
				Type:        result.Check,
				JobID:       jobID,
				Status:      job.StatusOK,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
			},
		},
	}
}

func NewCheckResultWA(jobID job.ID, elapsedTime int, usedMemory int) Result {
	return Result{
		&CheckResult{
			Details: result.Details{
				Type:        result.Check,
				JobID:       jobID,
				Status:      job.StatusWA,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
			},
		},
	}
}

func NewCheckResultErr(jobID job.ID, err string, elapsedTime int, usedMemory int) Result {
	return Result{
		&CheckResult{
			Details: result.Details{
				Type:        result.Check,
				JobID:       jobID,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
				Error:       err,
			},
		},
	}
}
