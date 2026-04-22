package jobs

import (
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/output"
)

type CheckCppJob struct {
	job.Details
	CompiledChecker input.Input `json:"compiled_checker"`
	TestInput       input.Input `json:"test_input"`
	CorrectOutput   input.Input `json:"correct_output"`
	SuspectOutput   input.Input `json:"suspect_output"`
}

func NewCheckCppJob(
	id job.ID,
	successStatus job.Status,
	timeLimit int,
	memoryLimit int,
	expectedTime int,
	expectedMemory int,
	compiledChecker input.Input,
	testInput input.Input,
	correctOutput input.Input,
	suspectOutput input.Input,
) Job {
	return Job{
		&CheckCppJob{
			Details: job.Details{
				ID:             id,
				Type:           job.CheckCpp,
				SuccessStatus:  successStatus,
				TimeLimit:      timeLimit,
				MemoryLimit:    memoryLimit,
				ExpectedTime:   expectedTime,
				ExpectedMemory: expectedMemory,
			},
			CompiledChecker: compiledChecker,
			TestInput:       testInput,
			CorrectOutput:   correctOutput,
			SuspectOutput:   suspectOutput,
		},
	}
}

func (jb *CheckCppJob) GetInputs() []input.Input {
	return []input.Input{jb.CompiledChecker, jb.TestInput, jb.CorrectOutput, jb.SuspectOutput}
}

func (jb *CheckCppJob) GetOutput() *output.Output {
	return nil
}

func (jb *CheckCppJob) GetDependencies() []job.ID {
	return getDependencies(jb.GetInputs())
}
