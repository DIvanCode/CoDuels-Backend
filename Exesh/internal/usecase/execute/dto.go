package execute

import "exesh/internal/domain/execution"

type (
	Command struct {
		Steps []execution.Step
	}

	Result struct {
		ExecutionID execution.ID
	}
)
