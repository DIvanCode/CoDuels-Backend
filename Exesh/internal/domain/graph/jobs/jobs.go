package jobs

import (
	"encoding/json"
	"exesh/internal/domain/graph"
	"exesh/internal/domain/graph/inputs"
	"fmt"
)

func UnmarshalJSON(data []byte) (job graph.Job, err error) {
	var details graph.JobDetails
	if err = json.Unmarshal(data, &details); err != nil {
		err = fmt.Errorf("failed to unmarshal job etails: %w", err)
		return
	}

	switch details.Type {
	case graph.CompileCppJobType:
		job = &CompileCppJob{}
	case graph.RunCppJobType:
		job = &RunCppJob{}
	case graph.RunGoJobType:
		job = &RunGoJob{}
	case graph.RunPyJobType:
		job = &RunPyJob{}
	case graph.CheckCppJobType:
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

func UnmarshalJobsJSON(data []byte) (jobsArray []graph.Job, err error) {
	var array []json.RawMessage
	if err = json.Unmarshal(data, &array); err != nil {
		err = fmt.Errorf("failed to unmarshal jobs array: %w", err)
		return
	}

	jobsArray = make([]graph.Job, 0, len(array))
	for _, item := range array {
		var job graph.Job
		job, err = UnmarshalJSON(item)
		if err != nil {
			err = fmt.Errorf("failed to unmarshal job: %w", err)
			return
		}
		jobsArray = append(jobsArray, job)
	}
	return
}

func getJobDependencies(job graph.Job) []graph.JobID {
	dependencies := make(map[graph.JobID]any)
	for _, input := range job.GetInputs() {
		if artifactInput, ok := input.(inputs.ArtifactInput); ok {
			dependencies[artifactInput.JobID] = struct{}{}
		}
	}
	result := make([]graph.JobID, 0, len(dependencies))
	for id := range dependencies {
		result = append(result, id)
	}
	return result
}
