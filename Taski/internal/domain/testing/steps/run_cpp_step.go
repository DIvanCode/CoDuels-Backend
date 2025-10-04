package steps

import "taski/internal/domain/testing"

type RunCppStep struct {
	testing.StepDetails
	CompiledCode testing.Source `json:"compiled_code"`
	RunInput     testing.Source `json:"run_input"`
	TimeLimit    int            `json:"time_limit"`
	MemoryLimit  int            `json:"memory_limit"`
}

func NewRunCppStep(name string, compiledCode, runInput testing.Source, timeLimit, memoryLimit int) RunCppStep {
	return RunCppStep{
		StepDetails: testing.StepDetails{
			Name: name,
			Type: testing.RunCpp,
		},
		CompiledCode: compiledCode,
		RunInput:     runInput,
		TimeLimit:    timeLimit,
		MemoryLimit:  memoryLimit,
	}
}
