package inputs

import "exesh/internal/domain/execution"

type ArtifactInput struct {
	execution.InputDetails
	JobID    execution.JobID `json:"job_id"`
	File     string          `json:"file"`
	WorkerID string          `json:"worker_id"`
}

func NewArtifactInput(file string, jobID execution.JobID, workerID string) ArtifactInput {
	return ArtifactInput{
		InputDetails: execution.InputDetails{
			Type: execution.ArtifactInputType,
			File: file,
		},
		JobID:    jobID,
		WorkerID: workerID,
	}
}
