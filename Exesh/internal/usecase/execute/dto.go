package execute

import (
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/source/sources"
)

type (
	Command struct {
		Sources sources.Definitions
		Stages  execution.StageDefinitions
	}

	Result struct {
		ExecutionID execution.ID
	}
)
