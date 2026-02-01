package execute

import (
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/source/sources"
)

type (
	Command struct {
		Sources []sources.Definition
		Stages  []execution.StageDefinition
	}

	Result struct {
		ExecutionID execution.ID
	}
)
