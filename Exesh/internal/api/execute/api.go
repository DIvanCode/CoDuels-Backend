package execute

import (
	"exesh/internal/api"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/source/sources"
)

type (
	Request struct {
		Sources []sources.Definition        `json:"sources"`
		Stages  []execution.StageDefinition `json:"stages"`
	}

	Response struct {
		api.Response
		ExecutionID *execution.ID `json:"execution_id,omitempty"`
	}
)
