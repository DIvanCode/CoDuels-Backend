package jobs

import (
	"taski/internal/domain/testing/input/inputs"
	"taski/internal/domain/testing/job"
)

type CheckCppJob struct {
	job.Details
	CompiledChecker inputs.Input `json:"compiled_checker"`
	TestInput       inputs.Input `json:"test_input"`
	CorrectOutput   inputs.Input `json:"correct_output"`
	SuspectOutput   inputs.Input `json:"suspect_output"`
}

func NewCheckCppJob(
	name job.Name,
	successStatus job.Status,
	categoryName string,
	timeLimit int,
	memoryLimit int,
	compiledChecker inputs.Input,
	testInput inputs.Input,
	correctOutput inputs.Input,
	suspectOutput inputs.Input,
) Job {
	return Job{IJob: &CheckCppJob{
		Details: job.Details{
			Type:          job.CheckCpp,
			Name:          name,
			SuccessStatus: successStatus,
			CategoryName:  categoryName,
			TimeLimit:     timeLimit,
			MemoryLimit:   memoryLimit,
		},
		CompiledChecker: compiledChecker,
		TestInput:       testInput,
		CorrectOutput:   correctOutput,
		SuspectOutput:   suspectOutput,
	}}
}
