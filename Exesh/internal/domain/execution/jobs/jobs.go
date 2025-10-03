package jobs

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"fmt"
)

func UnmarshalJSON(data []byte) (job execution.Job, err error) {
	var details execution.JobDetails
	if err = json.Unmarshal(data, &details); err != nil {
		err = fmt.Errorf("failed to unmarshal job etails: %w", err)
		return
	}

	switch details.Type {
	case execution.CompileCppJobType:
		job = &CompileCppJob{}
	case execution.RunCppJobType:
		job = &RunCppJob{}
	case execution.RunGoJobType:
		job = &RunGoJob{}
	case execution.RunPyJobType:
		job = &RunPyJob{}
	case execution.CheckCppJobType:
		job = &CheckCppJob{}
	default:
		err = fmt.Errorf("unknown job type: %s", details.Type)
		return
	}

	if err = json.Unmarshal(data, job); err != nil {
		err = fmt.Errorf("failed to unmarshal %s job: %w", details.Type, err)
		return
	}
	return
}

func UnmarshalJobsJSON(data []byte) (jobsArray []execution.Job, err error) {
	var array []json.RawMessage
	if err = json.Unmarshal(data, &array); err != nil {
		err = fmt.Errorf("failed to unmarshal jobs array: %w", err)
		return
	}

	jobsArray = make([]execution.Job, 0, len(array))
	for _, item := range array {
		var job execution.Job
		job, err = UnmarshalJSON(item)
		if err != nil {
			err = fmt.Errorf("failed to unmarshal job: %w", err)
			return
		}
		jobsArray = append(jobsArray, job)
	}
	return
}
