package sources

import (
	"exesh/internal/domain/execution"
)

type OtherStepSource struct {
	execution.SourceDetails
	StepName string `json:"step_name"`
}
