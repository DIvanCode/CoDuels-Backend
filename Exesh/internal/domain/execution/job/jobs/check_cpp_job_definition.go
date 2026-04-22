package jobs

import (
	"exesh/internal/domain/execution/input/inputs"
	"exesh/internal/domain/execution/job"
)

type CheckCppJobDefinition struct {
	job.DefinitionDetails
	CompiledChecker inputs.Definition `json:"compiled_checker"`
	TestInput       inputs.Definition `json:"test_input"`
	CorrectOutput   inputs.Definition `json:"correct_output"`
	SuspectOutput   inputs.Definition `json:"suspect_output"`
}
