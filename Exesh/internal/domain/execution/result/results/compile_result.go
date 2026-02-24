package results

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/result"
	"time"
)

type CompileResult struct {
	result.Details
	CompilationError *string `json:"compilation_error,omitempty"`
}

func NewCompileResultOK(jobID job.ID) Result {
	return Result{
		&CompileResult{
			Details: result.Details{
				Type:   result.Compile,
				JobID:  jobID,
				Status: job.StatusOK,
				DoneAt: time.Now(),
			},
		},
	}
}

func NewCompileResultCE(jobID job.ID, compilationError string) Result {
	return Result{
		&CompileResult{
			Details: result.Details{
				Type:   result.Compile,
				JobID:  jobID,
				Status: job.StatusCE,
				DoneAt: time.Now(),
			},
			CompilationError: &compilationError,
		},
	}
}

func NewCompileResultErr(jobID job.ID, err string) Result {
	return Result{
		&CompileResult{
			Details: result.Details{
				Type:   result.Compile,
				JobID:  jobID,
				DoneAt: time.Now(),
				Error:  err,
			},
		},
	}
}
