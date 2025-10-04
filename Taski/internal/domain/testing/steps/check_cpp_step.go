package steps

import "taski/internal/domain/testing"

type CheckCppStep struct {
	testing.StepDetails
	CompiledChecker testing.Source `json:"compiled_checker"`
	CorrectOutput   testing.Source `json:"correct_output"`
	SuspectOutput   testing.Source `json:"suspect_output"`
}

func NewCheckCppStep(name string, compiledChecker, correctOutput, suspectOutput testing.Source) CheckCppStep {
	return CheckCppStep{
		StepDetails: testing.StepDetails{
			Name: name,
			Type: testing.CheckCpp,
		},
		CompiledChecker: compiledChecker,
		CorrectOutput:   correctOutput,
		SuspectOutput:   suspectOutput,
	}
}
