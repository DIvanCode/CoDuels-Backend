package steps

import "taski/internal/domain/testing"

type RunGoStep struct {
	testing.StepDetails
	Code        testing.Source `json:"code"`
	RunInput    testing.Source `json:"run_input"`
	TimeLimit   int            `json:"time_limit"`
	MemoryLimit int            `json:"memory_limit"`
}

func NewRunGoStep(name string, code, runInput testing.Source, timeLimit, memoryLimit int) RunGoStep {
	return RunGoStep{
		StepDetails: testing.StepDetails{
			Name: name,
			Type: testing.RunGo,
		},
		Code:        code,
		RunInput:    runInput,
		TimeLimit:   timeLimit,
		MemoryLimit: memoryLimit,
	}
}
