package jobs

import (
	"encoding/json"
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
	"fmt"
)

type Job struct {
	job.IJob
}

func (jb *Job) UnmarshalJSON(data []byte) error {
	var details job.Details
	if err := json.Unmarshal(data, &details); err != nil {
		return fmt.Errorf("failed to unmarshal job details: %w", err)
	}

	switch details.Type {
	case job.CompileCpp:
		jb.IJob = &CompileCppJob{}
	case job.CompileGo:
		jb.IJob = &CompileGoJob{}
	case job.RunCpp:
		jb.IJob = &RunCppJob{}
	case job.RunGo:
		jb.IJob = &RunGoJob{}
	case job.RunPy:
		jb.IJob = &RunPyJob{}
	case job.CheckCpp:
		jb.IJob = &CheckCppJob{}
	default:
		return fmt.Errorf("unknown job type: %s", details.Type)
	}

	if err := json.Unmarshal(data, jb.IJob); err != nil {
		return fmt.Errorf("failed to unmarshal %s job: %w", details.Type, err)
	}

	return nil
}

func (jb *Job) AsCompileCpp() *CompileCppJob {
	return jb.IJob.(*CompileCppJob)
}

func (jb *Job) AsCompileGo() *CompileGoJob {
	return jb.IJob.(*CompileGoJob)
}

func (jb *Job) AsRunCpp() *RunCppJob {
	return jb.IJob.(*RunCppJob)
}

func (jb *Job) AsRunGo() *RunGoJob {
	return jb.IJob.(*RunGoJob)
}

func (jb *Job) AsRunPy() *RunPyJob {
	return jb.IJob.(*RunPyJob)
}

func (jb *Job) AsCheckCpp() *CheckCppJob {
	return jb.IJob.(*CheckCppJob)
}

func getDependencies(ins []input.Input) []job.ID {
	deps := make([]job.ID, 0)
	for _, in := range ins {
		if in.Type == input.Artifact {
			var jobID job.ID
			if err := jobID.FromString(in.SourceID.String()); err == nil {
				deps = append(deps, jobID)
			}
		}
	}
	return deps
}
