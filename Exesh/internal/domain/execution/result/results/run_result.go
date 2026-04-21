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

func NewRunResultOK(jobID job.ID, elapsedTime int, usedMemory int) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:        result.Run,
				JobID:       jobID,
				Status:      job.StatusOK,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
			},
			HasOutput: false,
		},
	}
}

func NewRunResultWithOutput(jobID job.ID, out string, elapsedTime int, usedMemory int) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:        result.Run,
				JobID:       jobID,
				Status:      job.StatusOK,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
			},
			HasOutput: true,
			Output:    out,
		},
	}
}

func NewRunResultTL(jobID job.ID, elapsedTime int, usedMemory int) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:        result.Run,
				JobID:       jobID,
				Status:      job.StatusTL,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
			},
			HasOutput: false,
		},
	}
}

func NewRunResultML(jobID job.ID, elapsedTime int, usedMemory int) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:        result.Run,
				JobID:       jobID,
				Status:      job.StatusML,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
			},
			HasOutput: false,
		},
	}
}

func NewRunResultRE(jobID job.ID, elapsedTime int, usedMemory int) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:        result.Run,
				JobID:       jobID,
				Status:      job.StatusRE,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
			},
			HasOutput: false,
		},
	}
}

func NewRunResultErr(jobID job.ID, err string, elapsedTime int, usedMemory int) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:        result.Run,
				JobID:       jobID,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
				Error:       err,
			},
			HasOutput: false,
		},
	}
}
