package execute

import (
	"taski/internal/domain/testing"
)

type (
	Request struct {
		Steps []testing.Step `json:"steps"`
	}

	Response struct {
		ExecutionID testing.ExecutionID `json:"execution_id"`
	}
)
