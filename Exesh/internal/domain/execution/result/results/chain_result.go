package results

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/result"
	"time"
)

type ChainResult struct {
	result.Details
	Results []Result `json:"results"`
}

func NewChainResult(jobID job.ID, status job.Status, results []Result) Result {
	return Result{
		&ChainResult{
			Details: result.Details{
				Type:   result.Chain,
				JobID:  jobID,
				Status: status,
				DoneAt: time.Now(),
			},
			Results: results,
		},
	}
}

func NewChainResultErr(jobID job.ID, err string, results []Result) Result {
	return Result{
		&ChainResult{
			Details: result.Details{
				Type:   result.Chain,
				JobID:  jobID,
				DoneAt: time.Now(),
				Error:  err,
			},
			Results: results,
		},
	}
}
