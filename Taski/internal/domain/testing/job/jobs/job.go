package jobs

import (
	"encoding/json"
	"fmt"
	"taski/internal/domain/testing/job"
)

type Job struct {
	job.IJob
}

func (jb Job) MarshalJSON() ([]byte, error) {
	if jb.IJob == nil {
		return []byte("null"), nil
	}

	return json.Marshal(jb.IJob)
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
