package jobs

import (
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/output"
)

type RunGoJob struct {
	job.Details
	CompiledCode input.Input   `json:"compiled_code"`
	RunInput     input.Input   `json:"run_input"`
	RunOutput    output.Output `json:"run_output"`
	TimeLimit    int           `json:"time_limit"`
	MemoryLimit  int           `json:"memory_limit"`
	ShowOutput   bool          `json:"show_output"`
}

func NewRunGoJob(
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
		&RunGoJob{
			Details: job.Details{
				ID:            id,
				Type:          job.RunGo,
				SuccessStatus: successStatus,
			},
			CompiledCode: code,
			RunInput:     runInput,
			RunOutput:    runOutput,
			TimeLimit:    timeLimit,
			MemoryLimit:  memoryLimit,
			ShowOutput:   showOutput,
		},
	}
}

func (jb *RunGoJob) GetInputs() []input.Input {
	return []input.Input{jb.CompiledCode, jb.RunInput}
}

func (jb *RunGoJob) GetOutput() *output.Output {
	return &jb.RunOutput
}

func (jb *RunGoJob) GetDependencies() []job.ID {
	return getDependencies(jb.GetInputs())
}
