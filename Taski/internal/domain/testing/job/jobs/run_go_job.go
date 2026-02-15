package jobs

import (
	"taski/internal/domain/testing/input/inputs"
	"taski/internal/domain/testing/job"
)

type RunGoJob struct {
	job.Details
	CompiledCode inputs.Input `json:"compiled_code"`
	RunInput     inputs.Input `json:"input"`
	TimeLimit    int          `json:"time_limit"`
	MemoryLimit  int          `json:"memory_limit"`
	ShowOutput   bool         `json:"show_output"`
}

func NewRunGoJob(name job.Name, compiledCode inputs.Input, input inputs.Input, timeLimit int, memoryLimit int, showOutput bool) Job {
	return Job{IJob: &RunGoJob{
		Details: job.Details{
			Type:          job.RunGo,
			Name:          name,
			SuccessStatus: job.StatusOK,
		},
		CompiledCode: compiledCode,
		RunInput:     input,
		TimeLimit:    timeLimit,
		MemoryLimit:  memoryLimit,
		ShowOutput:   showOutput,
	}}
}
