package execute

import (
	"taski/internal/domain/testing/execution"
	"taski/internal/domain/testing/source/sources"
)

type (
	Request struct {
		Stages  execution.Stages `json:"stages"`
		Sources sources.Sources  `json:"sources"`
	}

	Response struct {
		ExecutionID execution.ID `json:"execution_id"`
	}
)
