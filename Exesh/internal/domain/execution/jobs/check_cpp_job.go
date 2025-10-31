package jobs

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/inputs"
	"fmt"
)

type CheckCppJob struct {
	execution.JobDetails
	CompiledChecker execution.Input `json:"compiled_checker"`
	CorrectOutput   execution.Input `json:"correct_output"`
	SuspectOutput   execution.Input `json:"suspect_output"`
}

func NewCheckCppJob(
	id execution.JobID,
	compiledChecker execution.Input,
	correctOutput execution.Input,
	suspectOutput execution.Input,
) CheckCppJob {
	return CheckCppJob{
		JobDetails: execution.JobDetails{
			ID:   id,
			Type: execution.CheckCppJobType,
		},
		CompiledChecker: compiledChecker,
		CorrectOutput:   correctOutput,
		SuspectOutput:   suspectOutput,
	}
}

func (job CheckCppJob) GetInputs() []execution.Input {
	return []execution.Input{job.CompiledChecker, job.CorrectOutput, job.SuspectOutput}
}

func (job CheckCppJob) GetOutput() execution.Output {
	return nil
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
