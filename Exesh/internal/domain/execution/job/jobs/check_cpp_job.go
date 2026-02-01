package jobs

import (
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/output"
)

type CheckCppJob struct {
	job.Details
	CompiledChecker input.Input `json:"compiled_checker"`
	CorrectOutput   input.Input `json:"correct_output"`
	SuspectOutput   input.Input `json:"suspect_output"`
}

func NewCheckCppJob(
	id job.ID,
	successStatus job.Status,
	compiledChecker input.Input,
	correctOutput input.Input,
	suspectOutput input.Input,
) Job {
	return Job{
		&CheckCppJob{
			Details: job.Details{
				ID:            id,
				Type:          job.CheckCpp,
				SuccessStatus: successStatus,
			},
			CompiledChecker: compiledChecker,
			CorrectOutput:   correctOutput,
			SuspectOutput:   suspectOutput,
		},
	}
}

func (jb *CheckCppJob) GetInputs() []input.Input {
	return []input.Input{jb.CompiledChecker, jb.CorrectOutput, jb.SuspectOutput}
}

func (jb *CheckCppJob) GetOutput() *output.Output {
	return nil
}

func (jb *CheckCppJob) GetDependencies() []job.ID {
	return getDependencies(jb.GetInputs())
}
