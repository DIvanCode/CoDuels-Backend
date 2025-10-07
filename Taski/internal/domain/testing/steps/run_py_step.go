package steps

import "taski/internal/domain/testing"

type RunPyStep struct {
	testing.StepDetails
	Code        testing.Source `json:"code"`
	RunInput    testing.Source `json:"run_input"`
	TimeLimit   int            `json:"time_limit"`
	MemoryLimit int            `json:"memory_limit"`
}

func NewRunPyStep(name string, code, runInput testing.Source, timeLimit, memoryLimit int) RunPyStep {
	return RunPyStep{
		StepDetails: testing.StepDetails{
			Name: name,
			Type: testing.RunPy,
		},
		Code:        code,
		RunInput:    runInput,
		TimeLimit:   timeLimit,
		MemoryLimit: memoryLimit,
	}
}
