package results

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/result"
	"time"
)

type RunResult struct {
	result.Details
	HasOutput bool   `json:"has_output"`
	Output    string `json:"output,omitempty"`
}

func NewRunResultOK(jobID job.ID) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:   result.Run,
				ID:     jobID,
				Status: job.StatusOK,
				DoneAt: time.Now(),
			},
			HasOutput: false,
		},
	}
}

func NewRunResultWithOutput(jobID job.ID, out string) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:   result.Run,
				ID:     jobID,
				Status: job.StatusOK,
				DoneAt: time.Now(),
			},
			HasOutput: true,
			Output:    out,
		},
	}
}

func NewRunResultTL(jobID job.ID) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:   result.Run,
				ID:     jobID,
				Status: job.StatusTL,
				DoneAt: time.Now(),
			},
			HasOutput: false,
		},
	}
}

func NewRunResultML(jobID job.ID) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:   result.Run,
				ID:     jobID,
				Status: job.StatusML,
				DoneAt: time.Now(),
			},
			HasOutput: false,
		},
	}
}

func NewRunResultRE(jobID job.ID) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:   result.Run,
				ID:     jobID,
				Status: job.StatusRE,
				DoneAt: time.Now(),
			},
			HasOutput: false,
		},
	}
}

func NewRunResultErr(jobID job.ID, err string) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:   result.Run,
				ID:     jobID,
				DoneAt: time.Now(),
				Error:  err,
			},
			HasOutput: false,
		},
	}
}
