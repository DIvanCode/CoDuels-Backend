package jobs

import (
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/output"
)

type CompileGoJob struct {
	job.Details
	Code         input.Input   `json:"code"`
	CompiledCode output.Output `json:"compiled_code"`
}

func NewCompileGoJob(
	id job.ID,
	successStatus job.Status,
	code input.Input,
	compiledCode output.Output,
) Job {
	return Job{
		&CompileGoJob{
			Details: job.Details{
				ID:            id,
				Type:          job.CompileGo,
				SuccessStatus: successStatus,
			},
			Code:         code,
			CompiledCode: compiledCode,
		},
	}
}

func (jb *CompileGoJob) GetInputs() []input.Input {
	return []input.Input{jb.Code}
}

func (jb *CompileGoJob) GetOutput() *output.Output {
	return &jb.CompiledCode
}

func (jb *CompileGoJob) GetDependencies() []job.ID {
	return getDependencies(jb.GetInputs())
}
