package jobs

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/inputs"
	"exesh/internal/domain/execution/outputs"
	"fmt"
)

type RunCppJob struct {
	execution.JobDetails
	CompiledCode execution.Input  `json:"compiled_code"`
	RunInput     execution.Input  `json:"run_input"`
	RunOutput    execution.Output `json:"run_output"`
	TimeLimit    int              `json:"time_limit"`
	MemoryLimit  int              `json:"memory_limit"`
	ShowOutput   bool             `json:"show_output"`
}

func NewRunCppJob(
	id execution.JobID,
	compiledCode execution.Input,
	runInput execution.Input,
	runOutput execution.Output,
	timeLimit int,
	memoryLimit int,
	showOutput bool) RunCppJob {
	return RunCppJob{
		JobDetails: execution.JobDetails{
			ID:   id,
			Type: execution.RunCppJobType,
		},
		CompiledCode: compiledCode,
		RunInput:     runInput,
		RunOutput:    runOutput,
		TimeLimit:    timeLimit,
		MemoryLimit:  memoryLimit,
		ShowOutput:   showOutput,
	}
}

func (job RunCppJob) GetInputs() []execution.Input {
	return []execution.Input{job.CompiledCode, job.RunInput}
}

func (job RunCppJob) GetOutput() execution.Output {
	return job.RunOutput
}

func (job *RunCppJob) UnmarshalJSON(data []byte) error {
	var err error
	if err = json.Unmarshal(data, &job.JobDetails); err != nil {
		return fmt.Errorf("failed to unmarshal details: %w", err)
	}

	attributes := struct {
		CompiledCode json.RawMessage `json:"compiled_code"`
		RunInput     json.RawMessage `json:"run_input"`
		RunOutput    json.RawMessage `json:"run_output"`
		TimeLimit    int             `json:"time_limit"`
		MemoryLimit  int             `json:"memory_limit"`
		ShowOutput   bool            `json:"show_output"`
	}{}
	if err = json.Unmarshal(data, &attributes); err != nil {
		return fmt.Errorf("failed to unmarshal %s job attributes: %w", job.Type, err)
	}

	if job.CompiledCode, err = inputs.UnmarshalInputJSON(attributes.CompiledCode); err != nil {
		return fmt.Errorf("failed to unmarshal compiled_code input: %w", err)
	}
	if job.RunInput, err = inputs.UnmarshalInputJSON(attributes.RunInput); err != nil {
		return fmt.Errorf("failed to unmarshal run_input input: %w", err)
	}
	if job.RunOutput, err = outputs.UnmarshalOutputJSON(attributes.RunOutput); err != nil {
		return fmt.Errorf("failed to unmarshal run_output output: %w", err)
	}
	job.TimeLimit = attributes.TimeLimit
	job.MemoryLimit = attributes.MemoryLimit
	job.ShowOutput = attributes.ShowOutput

	return nil
}
