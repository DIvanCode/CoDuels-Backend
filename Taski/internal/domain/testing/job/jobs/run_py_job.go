package jobs

import (
	"taski/internal/domain/testing/input/inputs"
	"taski/internal/domain/testing/job"
)

type RunPyJob struct {
	job.Details
	Code        inputs.Input `json:"code"`
	RunInput    inputs.Input `json:"input"`
	TimeLimit   int          `json:"time_limit"`
	MemoryLimit int          `json:"memory_limit"`
	ShowOutput  bool         `json:"show_output"`
}

func NewRunPyJob(name job.Name, code inputs.Input, input inputs.Input, timeLimit int, memoryLimit int, showOutput bool) Job {
	return Job{IJob: &RunPyJob{
		Details: job.Details{
			Type:          job.RunPy,
			Name:          name,
			SuccessStatus: job.StatusOK,
		},
		Code:        code,
		RunInput:    input,
		TimeLimit:   timeLimit,
		MemoryLimit: memoryLimit,
		ShowOutput:  showOutput,
	}}
}
