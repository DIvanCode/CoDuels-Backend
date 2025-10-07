package heartbeat

import (
	"encoding/json"
	"exesh/internal/api"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/jobs"
	"exesh/internal/domain/execution/results"
	"fmt"
)

type (
	Request struct {
		WorkerID string             `json:"worker_id"`
		DoneJobs []execution.Result `json:"done_jobs"`
		// AddedArtifacts []ArtifactDto      `json:"added_artifacts"`
		FreeSlots int `json:"free_slots"`
	}

	Response struct {
		api.Response
		Jobs []execution.Job `json:"jobs,omitempty"`
	}

	// ArtifactDto struct {
	// 	JobID     execution.JobID `json:"job_id"`
	// 	TrashTime time.Time       `json:"trash_time"`
	// }
)

func (r *Request) UnmarshalJSON(data []byte) (err error) {
	attributes := struct {
		WorkerID  string          `json:"worker_id"`
		DoneJobs  json.RawMessage `json:"done_jobs"`
		FreeSlots int             `json:"free_slots"`
	}{}
	if err = json.Unmarshal(data, &attributes); err != nil {
		return fmt.Errorf("failed to unmarshal request attributes: %w", err)
	}

	r.WorkerID = attributes.WorkerID
	if r.DoneJobs, err = results.UnmarshalResultsJSON(attributes.DoneJobs); err != nil {
		return fmt.Errorf("failed to unmarshal results: %w", err)
	}
	r.FreeSlots = attributes.FreeSlots
	return nil
}

func (r *Response) UnmarshalJSON(data []byte) (err error) {
	attributes := struct {
		api.Response
		Jobs json.RawMessage `json:"jobs,omitempty"`
	}{}
	if err = json.Unmarshal(data, &attributes); err != nil {
		return fmt.Errorf("failed to unmarshal response attributes: %w", err)
	}

	r.Response = attributes.Response
	if attributes.Jobs != nil {
		if r.Jobs, err = jobs.UnmarshalJobsJSON(attributes.Jobs); err != nil {
			return err
		}
	} else {
		r.Jobs = []execution.Job{}
	}
	return nil
}
