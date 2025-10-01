package results

import (
	"exesh/internal/domain/execution"
)

type (
	RunResult struct {
		execution.ResultDetails
		Status    RunStatus `json:"status"`
		HasOutput bool      `json:"has_output"`
		Output    string    `json:"output,omitempty"`
	}

	RunStatus string
)

const (
	RunStatusOK RunStatus = "OK"
	RunStatusRE RunStatus = "RE"
	RunStatusTL RunStatus = "TL"
	RunStatusML RunStatus = "ML"
)
