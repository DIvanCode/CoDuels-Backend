package results

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/result"
	"time"
)

type RunResult struct {
	result.Details
	Output string `json:"output,omitempty"`
}

func NewRunResultOK(jobID job.ID, hasOutput bool, elapsedTime int, usedMemory int) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:        result.Run,
				JobID:       jobID,
				Status:      job.StatusOK,
				HasOutput:   hasOutput,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
			},
		},
	}
}

func NewRunResultWithOutput(jobID job.ID, hasOutput bool, out string, elapsedTime int, usedMemory int) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:        result.Run,
				JobID:       jobID,
				Status:      job.StatusOK,
				HasOutput:   hasOutput,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
			},
			Output: out,
		},
	}
}

func NewRunResultTL(jobID job.ID, hasOutput bool, elapsedTime int, usedMemory int) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:        result.Run,
				JobID:       jobID,
				Status:      job.StatusTL,
				HasOutput:   hasOutput,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
			},
		},
	}
}

func NewRunResultML(jobID job.ID, hasOutput bool, elapsedTime int, usedMemory int) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:        result.Run,
				JobID:       jobID,
				Status:      job.StatusML,
				HasOutput:   hasOutput,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
			},
		},
	}
}

func NewRunResultRE(jobID job.ID, hasOutput bool, elapsedTime int, usedMemory int) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:        result.Run,
				JobID:       jobID,
				Status:      job.StatusRE,
				HasOutput:   hasOutput,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
			},
		},
	}
}

func NewRunResultErr(jobID job.ID, err string, elapsedTime int, usedMemory int) Result {
	return Result{
		&RunResult{
			Details: result.Details{
				Type:        result.Run,
				JobID:       jobID,
				HasOutput:   false,
				DoneAt:      time.Now(),
				ElapsedTime: elapsedTime,
				UsedMemory:  usedMemory,
				Error:       err,
			},
		},
	}
}
