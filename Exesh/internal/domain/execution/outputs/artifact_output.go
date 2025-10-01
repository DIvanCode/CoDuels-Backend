package outputs

import "exesh/internal/domain/execution"

type ArtifactOutput struct {
	execution.OutputDetails
	JobID execution.JobID `json:"job_id"`
	File  string          `json:"file"`
}

func NewArtifactOutput(file string, jobID execution.JobID) ArtifactOutput {
	return ArtifactOutput{
		OutputDetails: execution.OutputDetails{
			Type: execution.ArtifactOutputType,
			File: file,
		},
		JobID: jobID,
	}
}
