package outputs

import (
	"exesh/internal/domain/graph"
	"exesh/internal/domain/graph/inputs"
)

type ArtifactOutput struct {
	graph.OutputDetails
	JobID graph.JobID `json:"job_id"`
	File  string      `json:"file"`
}

func NewArtifactOutput(jobID graph.JobID, file string) ArtifactOutput {
	return ArtifactOutput{
		OutputDetails: graph.OutputDetails{
			Type: graph.ArtifactOutputType,
		},
		JobID: jobID,
		File:  file,
	}
}

func (output ArtifactOutput) ConvertToInput() graph.Input {
	return inputs.NewArtifactInput(output.JobID, output.File)
}
