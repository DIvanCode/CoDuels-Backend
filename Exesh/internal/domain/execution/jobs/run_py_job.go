package jobs

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/inputs"
	"exesh/internal/domain/execution/outputs"
	"fmt"
)

type RunPyJob struct {
	execution.JobDetails
	Code        execution.Input  `json:"code"`
	RunInput    execution.Input  `json:"run_input"`
	RunOutput   execution.Output `json:"run_output"`
	TimeLimit   int              `json:"time_limit"`
	MemoryLimit int              `json:"memory_limit"`
	ShowOutput  bool             `json:"show_output"`
}

func NewRunPyJob(
	id execution.JobID,
	code execution.Input,
	runInput execution.Input,
	runOutput execution.Output,
	timeLimit int,
	memoryLimit int,
	showOutput bool) RunPyJob {
	return RunPyJob{
		JobDetails: execution.JobDetails{
			ID:   id,
			Type: execution.RunPyJobType,
		},
		Code:        code,
		RunInput:    runInput,
		RunOutput:   runOutput,
		TimeLimit:   timeLimit,
		MemoryLimit: memoryLimit,
		ShowOutput:  showOutput,
	}
}

func (job RunPyJob) GetInputs() []execution.Input {
	return []execution.Input{job.Code, job.RunInput}
}

func (job RunPyJob) GetOutput() execution.Output {
	return job.RunOutput
}

func (job *RunPyJob) UnmarshalJSON(data []byte) error {
	var err error
	if err = json.Unmarshal(data, &job.JobDetails); err != nil {
		return fmt.Errorf("failed to unmarshal details: %w", err)
	}

	attributes := struct {
		Code        json.RawMessage `json:"code"`
		RunInput    json.RawMessage `json:"run_input"`
		RunOutput   json.RawMessage `json:"run_output"`
		TimeLimit   int             `json:"time_limit"`
		MemoryLimit int             `json:"memory_limit"`
		ShowOutput  bool            `json:"show_output"`
	}{}
	if err = json.Unmarshal(data, &attributes); err != nil {
		return fmt.Errorf("failed to unmarshal %s job attributes: %w", job.Type, err)
	}

	if job.Code, err = inputs.UnmarshalInputJSON(attributes.Code); err != nil {
		return fmt.Errorf("failed to unmarshal code input: %w", err)
	}
	if job.RunInput, err = inputs.UnmarshalInputJSON(attributes.RunInput); err != nil {
		return fmt.Errorf("inputs to unmarshal run_input input: %w", err)
	}
	if job.RunOutput, err = outputs.UnmarshalOutputJSON(attributes.RunOutput); err != nil {
		return fmt.Errorf("failed to unmarshal run_output output: %w", err)
	}
	job.TimeLimit = attributes.TimeLimit
	job.MemoryLimit = attributes.MemoryLimit
	job.ShowOutput = attributes.ShowOutput

	return nil
}
