package results

import "exesh/internal/domain/execution"

type (
	CompileResult struct {
		execution.ResultDetails
		Status           CompileStatus `json:"status"`
		CompilationError string        `json:"compilation_error"`
	}

	CompileStatus string
)

const (
	CompileStatusOK CompileStatus = "OK"
	CompileStatusCE CompileStatus = "CE"
)

func (r CompileResult) ShouldFinishExecution() bool {
	return r.Status != CompileStatusOK
}
