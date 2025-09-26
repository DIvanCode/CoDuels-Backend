package jobs

import (
	"encoding/json"
	"exesh/internal/domain/graph"
	"exesh/internal/domain/graph/inputs"
	"exesh/internal/domain/graph/outputs"
	"fmt"
)

type CompileCppJob struct {
	graph.JobDetails
	Code         graph.Input  `json:"code"`
	CompiledCode graph.Output `json:"compiled_code"`
}

func NewCompileCppJob(id graph.JobID, code graph.Input, compiledCode graph.Output) CompileCppJob {
	return CompileCppJob{
		JobDetails: graph.JobDetails{
			ID:   id,
			Type: graph.CompileCppJobType,
		},
		Code:         code,
		CompiledCode: compiledCode,
	}
}

func (job CompileCppJob) GetDependencies() []graph.JobID {
	return getJobDependencies(job)
}

func (job CompileCppJob) GetInputs() []graph.Input {
	return []graph.Input{job.Code}
}

func (job CompileCppJob) GetOutput() graph.Output {
	return job.CompiledCode
}

func (job *CompileCppJob) UnmarshalJSON(data []byte) error {
	var err error
	if err = json.Unmarshal(data, &job.JobDetails); err != nil {
		return fmt.Errorf("failed to unmarshal details: %w", err)
	}

	attributes := struct {
		Code         json.RawMessage `json:"code"`
		CompiledCode json.RawMessage `json:"compiled_code"`
	}{}
	if err = json.Unmarshal(data, &attributes); err != nil {
		return fmt.Errorf("failed to unmarshal %s job attributes: %w", job.Type, err)
	}

	if job.Code, err = inputs.UnmarshalInputJSON(attributes.Code); err != nil {
		return fmt.Errorf("failed to unmarshal code input: %w", err)
	}
	if job.CompiledCode, err = outputs.UnmarshalOutputJSON(attributes.CompiledCode); err != nil {
		return fmt.Errorf("failed to unmarshal compiled_code output: %w", err)
	}

	return nil
}
