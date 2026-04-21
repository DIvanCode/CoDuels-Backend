package jobs

import (
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/output"
)

type RunPyJob struct {
	job.Details
	Code       input.Input   `json:"code"`
	RunInput   input.Input   `json:"run_input"`
	RunOutput  output.Output `json:"run_output"`
	ShowOutput bool          `json:"show_output"`
}

func NewRunPyJob(
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
		&RunPyJob{
			Details: job.Details{
				ID:             id,
				Type:           job.RunPy,
				SuccessStatus:  successStatus,
				TimeLimit:      timeLimit,
				MemoryLimit:    memoryLimit,
				ExpectedTime:   expectedTime,
				ExpectedMemory: expectedMemory,
			},
			Code:       code,
			RunInput:   runInput,
			RunOutput:  runOutput,
			ShowOutput: showOutput,
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
