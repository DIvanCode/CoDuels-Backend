package execution

type (
	Output interface {
		GetType() OutputType
		GetFile() string
	}

	OutputDetails struct {
		Type OutputType `json:"type"`
		File string     `json:"file"`
	}

	OutputType string
)

const (
	ArtifactOutputType OutputType = "artifact"
)

func (output OutputDetails) GetType() OutputType {
	return output.Type
}

func (output OutputDetails) GetFile() string {
	return output.File
}
