package inputs

import "exesh/internal/domain/graph"

type ArtifactInput struct {
	graph.InputDetails
	JobID graph.JobID `json:"job_id"`
	File  string      `json:"file"`
}

func NewArtifactInput(jobID graph.JobID, file string) ArtifactInput {
	return ArtifactInput{
		InputDetails: graph.InputDetails{
			Type: graph.ArtifactInputType,
		},
		JobID: jobID,
		File:  file,
	}
}
