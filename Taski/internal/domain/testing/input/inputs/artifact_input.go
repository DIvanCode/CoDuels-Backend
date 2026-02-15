package inputs

import (
	"taski/internal/domain/testing/input"
	"taski/internal/domain/testing/job"
)

type ArtifactInput struct {
	input.Details
	JobName job.Name `json:"job"`
}

func NewArtifactInput(jobName job.Name) Input {
	return Input{
		&ArtifactInput{
			Details: input.Details{Type: input.Artifact},
			JobName: jobName,
		},
	}
}
