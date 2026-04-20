package jobs

import (
	"taski/internal/domain/testing/input/inputs"
	"taski/internal/domain/testing/job"
)

type RunPyJob struct {
	job.Details
	Code       inputs.Input `json:"code"`
	RunInput   inputs.Input `json:"input"`
	ShowOutput bool         `json:"show_output"`
}

func NewRunPyJob(name job.Name, categoryName string, code inputs.Input, input inputs.Input, timeLimit int, memoryLimit int, showOutput bool) Job {
	return Job{IJob: &RunPyJob{
		Details: job.Details{
			Type:          job.RunPy,
			Name:          name,
			SuccessStatus: job.StatusOK,
			CategoryName:  categoryName,
			TimeLimit:     timeLimit,
			MemoryLimit:   memoryLimit,
		},
		Code:       code,
		RunInput:   input,
		ShowOutput: showOutput,
	}}
}
