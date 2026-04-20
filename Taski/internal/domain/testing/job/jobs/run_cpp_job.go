package jobs

import (
	"taski/internal/domain/testing/input/inputs"
	"taski/internal/domain/testing/job"
)

type RunCppJob struct {
	job.Details
	CompiledCode inputs.Input `json:"compiled_code"`
	RunInput     inputs.Input `json:"input"`
	ShowOutput   bool         `json:"show_output"`
}

func NewRunCppJob(name job.Name, categoryName string, compiledCode inputs.Input, input inputs.Input, timeLimit int, memoryLimit int, showOutput bool) Job {
	return Job{IJob: &RunCppJob{
		Details: job.Details{
			Type:          job.RunCpp,
			Name:          name,
			SuccessStatus: job.StatusOK,
			CategoryName:  categoryName,
			TimeLimit:     timeLimit,
			MemoryLimit:   memoryLimit,
		},
		CompiledCode: compiledCode,
		RunInput:     input,
		ShowOutput:   showOutput,
	}}
}
