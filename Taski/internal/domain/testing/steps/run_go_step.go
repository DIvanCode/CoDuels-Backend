package steps

import "taski/internal/domain/testing"

type RunGoStep struct {
	testing.StepDetails
	CompiledCode testing.Source `json:"compiled_code"`
	RunInput     testing.Source `json:"run_input"`
	TimeLimit    int            `json:"time_limit"`
	MemoryLimit  int            `json:"memory_limit"`
}

func NewRunGoStep(name string, compiledCode, runInput testing.Source, timeLimit, memoryLimit int) RunGoStep {
	return RunGoStep{
		StepDetails: testing.StepDetails{
			Name: name,
			Type: testing.RunGo,
		},
		CompiledCode: compiledCode,
		RunInput:     runInput,
		TimeLimit:    timeLimit,
		MemoryLimit:  memoryLimit,
	}
}
