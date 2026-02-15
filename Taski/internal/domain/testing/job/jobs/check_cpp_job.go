package jobs

import (
	"taski/internal/domain/testing/input/inputs"
	"taski/internal/domain/testing/job"
)

type CheckCppJob struct {
	job.Details
	CompiledChecker inputs.Input `json:"compiled_checker"`
	CorrectOutput   inputs.Input `json:"correct_output"`
	SuspectOutput   inputs.Input `json:"suspect_output"`
}

func NewCheckCppJob(name job.Name, successStatus job.Status, compiledChecker inputs.Input, correctOutput inputs.Input, suspectOutput inputs.Input) Job {
	return Job{IJob: &CheckCppJob{
		Details: job.Details{
			Type:          job.CheckCpp,
			Name:          name,
			SuccessStatus: successStatus,
		},
		CompiledChecker: compiledChecker,
		CorrectOutput:   correctOutput,
		SuspectOutput:   suspectOutput,
	}}
}
