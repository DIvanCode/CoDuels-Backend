package results

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/result"
	"time"
)

type CheckResult struct {
	result.Details
}

func NewCheckResultOK(jobID job.ID) Result {
	return Result{
		&CompileResult{
			Details: result.Details{
				Type:   result.Check,
				ID:     jobID,
				Status: job.StatusOK,
				DoneAt: time.Now(),
			},
		},
	}
}

func NewCheckResultWA(jobID job.ID) Result {
	return Result{
		&CompileResult{
			Details: result.Details{
				Type:   result.Check,
				ID:     jobID,
				Status: job.StatusWA,
				DoneAt: time.Now(),
			},
		},
	}
}

func NewCheckResultErr(jobID job.ID, err string) Result {
	return Result{
		&CompileResult{
			Details: result.Details{
				Type:   result.Check,
				ID:     jobID,
				DoneAt: time.Now(),
				Error:  err,
			},
		},
	}
}
