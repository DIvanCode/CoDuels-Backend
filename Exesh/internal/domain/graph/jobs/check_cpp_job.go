package jobs

import (
	"encoding/json"
	"exesh/internal/domain/graph"
	"exesh/internal/domain/graph/inputs"
	"fmt"
)

type CheckCppJob struct {
	graph.JobDetails
	CompiledChecker graph.Input  `json:"compiled_checker"`
	CorrectOutput   graph.Input  `json:"correct_output"`
	SuspectOutput   graph.Input  `json:"suspect_output"`
	CheckVerdict    graph.Output `json:"check_verdict"`
}

func NewCheckCppJob(
	id graph.JobID,
	compiledChecker graph.Input,
	correctOutput graph.Input,
	suspectOutput graph.Input,
	checkVerdict graph.Output,
) CheckCppJob {
	return CheckCppJob{
		JobDetails: graph.JobDetails{
			ID:   id,
			Type: graph.CheckCppJobType,
		},
		CompiledChecker: compiledChecker,
		CorrectOutput:   correctOutput,
		SuspectOutput:   suspectOutput,
		CheckVerdict:    checkVerdict,
	}
}

func (job CheckCppJob) GetDependencies() []graph.JobID {
	return getJobDependencies(job)
}

func (job CheckCppJob) GetInputs() []graph.Input {
	return []graph.Input{job.CompiledChecker, job.CorrectOutput, job.SuspectOutput}
}

func (job CheckCppJob) GetOutput() graph.Output {
	return job.CheckVerdict
}

func (job *CheckCppJob) UnmarshalJSON(data []byte) error {
	var err error
	if err = json.Unmarshal(data, &job.JobDetails); err != nil {
		return fmt.Errorf("failed to unmarshal details: %w", err)
	}

	attributes := struct {
		CompiledChecker json.RawMessage `json:"compiled_checker"`
		CorrectOutput   json.RawMessage `json:"correct_output"`
		SuspectOutput   json.RawMessage `json:"suspect_output"`
		CheckVerdict    json.RawMessage `json:"check_verdict"`
	}{}
	if err = json.Unmarshal(data, &attributes); err != nil {
		return fmt.Errorf("failed to unmarshal %s job attributes: %w", job.Type, err)
	}

	if job.CompiledChecker, err = inputs.UnmarshalInputJSON(attributes.CompiledChecker); err != nil {
		return fmt.Errorf("failed to unmarshal compiled_checker input: %w", err)
	}
	if job.CorrectOutput, err = inputs.UnmarshalInputJSON(attributes.CorrectOutput); err != nil {
		return fmt.Errorf("failed to unmarshal correct_output input: %w", err)
	}
	if job.SuspectOutput, err = inputs.UnmarshalInputJSON(attributes.SuspectOutput); err != nil {
		return fmt.Errorf("failed to unmarshal suspect_output input: %w", err)
	}

	return nil
}
