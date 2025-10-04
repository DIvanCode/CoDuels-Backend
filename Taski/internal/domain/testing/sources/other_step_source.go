package sources

import "taski/internal/domain/testing"

type OtherStepSource struct {
	testing.SourceDetails
	StepName string `json:"step_name"`
}

func NewOtherStepSource(stepName string) OtherStepSource {
	return OtherStepSource{
		SourceDetails: testing.SourceDetails{
			Type: testing.OtherStepSource,
		},
		StepName: stepName,
	}
}
