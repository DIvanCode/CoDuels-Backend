package jobs

import (
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/output"
)

type RunPyJob struct {
	job.Details
	Code        input.Input   `json:"code"`
	RunInput    input.Input   `json:"run_input"`
	RunOutput   output.Output `json:"run_output"`
	TimeLimit   int           `json:"time_limit"`
	MemoryLimit int           `json:"memory_limit"`
	ShowOutput  bool          `json:"show_output"`
}

func NewRunPyJob(
	id job.ID,
	successStatus job.Status,
	code input.Input,
	runInput input.Input,
	runOutput output.Output,
	timeLimit int,
	memoryLimit int,
	showOutput bool,
) Job {
	return Job{
		&RunPyJob{
			Details: job.Details{
				ID:            id,
				Type:          job.RunPy,
				SuccessStatus: successStatus,
			},
			Code:        code,
			RunInput:    runInput,
			RunOutput:   runOutput,
			TimeLimit:   timeLimit,
			MemoryLimit: memoryLimit,
			ShowOutput:  showOutput,
		},
	}
}

func (jb *RunPyJob) GetInputs() []input.Input {
	return []input.Input{jb.Code, jb.RunInput}
}

func (jb *RunPyJob) GetOutput() *output.Output {
	return &jb.RunOutput
}

func (jb *RunPyJob) GetDependencies() []job.ID {
	return getDependencies(jb.GetInputs())
}
