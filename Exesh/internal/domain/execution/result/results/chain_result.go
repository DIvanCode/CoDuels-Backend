package results

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/result"
	"time"
)

type ChainResult struct {
	result.Details
	Results map[string]Result `json:"results"`
}

func NewChainResult(jobID job.ID, status job.Status, inner []Result) Result {
	return Result{
		&ChainResult{
			Details: result.Details{
				Type:   result.Chain,
				JobID:  jobID,
				Status: status,
				DoneAt: time.Now(),
			},
			Results: innerResultsMap(inner),
		},
	}
}

func NewChainResultErr(jobID job.ID, inner []Result, err string) Result {
	return Result{
		&ChainResult{
			Details: result.Details{
				Type:   result.Chain,
				JobID:  jobID,
				DoneAt: time.Now(),
				Error:  err,
			},
			Results: innerResultsMap(inner),
		},
	}
}

func innerResultsMap(inner []Result) map[string]Result {
	resMap := make(map[string]Result, len(inner))
	for _, res := range inner {
		jobID := res.GetJobID()
		resMap[jobID.String()] = res
	}
	return resMap
}
