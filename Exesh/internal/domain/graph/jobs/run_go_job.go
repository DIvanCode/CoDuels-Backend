package jobs

import (
	"encoding/json"
	"exesh/internal/domain/graph"
	"exesh/internal/domain/graph/inputs"
	"exesh/internal/domain/graph/outputs"
	"fmt"
)

type RunGoJob struct {
	graph.JobDetails
	Code        graph.Input  `json:"code"`
	RunInput    graph.Input  `json:"run_input"`
	RunOutput   graph.Output `json:"run_output"`
	TimeLimit   int          `json:"time_limit"`
	MemoryLimit int          `json:"memory_limit"`
	ShowOutput  bool         `json:"show_output"`
}

func NewRunGoJob(
	id graph.JobID,
	code graph.Input,
	runInput graph.Input,
	runOutput graph.Output,
	timeLimit int,
	memoryLimit int,
	showOutput bool) RunGoJob {
	return RunGoJob{
		JobDetails: graph.JobDetails{
			ID:   id,
			Type: graph.RunGoJobType,
		},
		Code:        code,
		RunInput:    runInput,
		RunOutput:   runOutput,
		TimeLimit:   timeLimit,
		MemoryLimit: memoryLimit,
		ShowOutput:  showOutput,
	}
}

func (job RunGoJob) GetDependencies() []graph.JobID {
	return getJobDependencies(job)
}

func (job RunGoJob) GetInputs() []graph.Input {
	return []graph.Input{job.Code, job.RunInput}
}

func (job RunGoJob) GetOutput() graph.Output {
	return job.RunOutput
}

func (job *RunGoJob) UnmarshalJSON(data []byte) error {
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
		return fmt.Errorf("failed to unmarshal code Input: %w", err)
	}
	if job.RunInput, err = inputs.UnmarshalInputJSON(attributes.RunInput); err != nil {
		return fmt.Errorf("failed to unmarshal run_input Input: %w", err)
	}
	if job.RunOutput, err = outputs.UnmarshalOutputJSON(attributes.RunOutput); err != nil {
		return fmt.Errorf("failed to unmarshal run_output Output: %w", err)
	}
	job.TimeLimit = attributes.TimeLimit
	job.MemoryLimit = attributes.MemoryLimit
	job.ShowOutput = attributes.ShowOutput

	return nil
}
