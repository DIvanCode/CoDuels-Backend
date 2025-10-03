package results

import "exesh/internal/domain/execution"

type (
	CheckResult struct {
		execution.ResultDetails
		Status CheckStatus `json:"status"`
	}

	CheckStatus string
)

const (
	CheckStatusOK CheckStatus = "OK"
	CheckStatusWA CheckStatus = "WA"
)
