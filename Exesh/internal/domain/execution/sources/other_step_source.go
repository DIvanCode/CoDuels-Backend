package sources

import (
	"exesh/internal/domain/execution"
)

type OtherStepSource struct {
	execution.SourceDetails
	StepName execution.StepName `json:"step_name"`
}
