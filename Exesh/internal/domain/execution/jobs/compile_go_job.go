package jobs

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/inputs"
	"exesh/internal/domain/execution/outputs"
	"fmt"
)

type CompileGoJob struct {
	execution.JobDetails
	Code         execution.Input  `json:"code"`
	CompiledCode execution.Output `json:"compiled_code"`
}

func NewCompileGoJob(id execution.JobID, code execution.Input, compiledCode execution.Output) CompileGoJob {
	return CompileGoJob{
		JobDetails: execution.JobDetails{
			ID:   id,
			Type: execution.CompileGoJobType,
		},
		Code:         code,
		CompiledCode: compiledCode,
	}
}

func (job CompileGoJob) GetInputs() []execution.Input {
	return []execution.Input{job.Code}
}

func (job CompileGoJob) GetOutput() execution.Output {
	return job.CompiledCode
}

func (job *CompileGoJob) UnmarshalJSON(data []byte) error {
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
