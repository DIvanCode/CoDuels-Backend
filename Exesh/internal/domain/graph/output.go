package graph

type (
	Output interface {
		GetType() OutputType
		ConvertToInput() Input
	}

	OutputDetails struct {
		Type OutputType `json:"type"`
	}

	OutputType string
)

const (
	ArtifactOutputType OutputType = "artifact"
)

func (output OutputDetails) GetType() OutputType {
	return output.Type
}
