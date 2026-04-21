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
	ShowOutput   bool          `json:"show_output"`
}

func NewRunGoJob(
	id job.ID,
	successStatus job.Status,
	timeLimit int,
	memoryLimit int,
	expectedTime int,
	expectedMemory int,
	code input.Input,
	runInput input.Input,
	runOutput output.Output,
	showOutput bool,
) Job {
	return Job{
		&RunGoJob{
			Details: job.Details{
				ID:             id,
				Type:           job.RunGo,
				SuccessStatus:  successStatus,
				TimeLimit:      timeLimit,
				MemoryLimit:    memoryLimit,
				ExpectedTime:   expectedTime,
				ExpectedMemory: expectedMemory,
			},
			CompiledCode: code,
			RunInput:     runInput,
			RunOutput:    runOutput,
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
