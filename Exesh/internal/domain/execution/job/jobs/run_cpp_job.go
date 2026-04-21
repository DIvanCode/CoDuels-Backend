package jobs

import (
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/output"
)

type RunCppJob struct {
	job.Details
	CompiledCode input.Input   `json:"compiled_code"`
	RunInput     input.Input   `json:"run_input"`
	RunOutput    output.Output `json:"run_output"`
	ShowOutput   bool          `json:"show_output"`
}

func NewRunCppJob(
	id job.ID,
	successStatus job.Status,
	timeLimit int,
	memoryLimit int,
	expectedTime int,
	expectedMemory int,
	compiledCode input.Input,
	runInput input.Input,
	runOutput output.Output,
	showOutput bool,
) Job {
	return Job{
		&RunCppJob{
			Details: job.Details{
				ID:             id,
				Type:           job.RunCpp,
				SuccessStatus:  successStatus,
				TimeLimit:      timeLimit,
				MemoryLimit:    memoryLimit,
				ExpectedTime:   expectedTime,
				ExpectedMemory: expectedMemory,
			},
			CompiledCode: compiledCode,
			RunInput:     runInput,
			RunOutput:    runOutput,
			ShowOutput:   showOutput,
		},
	}
}

func (jb *RunCppJob) GetInputs() []input.Input {
	return []input.Input{jb.CompiledCode, jb.RunInput}
}

func (jb *RunCppJob) GetOutput() *output.Output {
	return &jb.RunOutput
}

func (jb *RunCppJob) GetDependencies() []job.ID {
	return getDependencies(jb.GetInputs())
}
