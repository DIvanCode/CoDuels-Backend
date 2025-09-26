package inputs

import (
	"exesh/internal/domain/execution"
)

type OtherStepInput struct {
	execution.InputDetails
	StepName string `json:"step_name"`
}
