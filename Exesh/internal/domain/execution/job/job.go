package job

import (
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/output"
)

type (
	IJob interface {
		GetType() Type
		GetID() ID
		GetSuccessStatus() Status
		GetTimeLimit() int
		GetMemoryLimit() int
		GetExpectedTime() int
		GetExpectedMemory() int
		GetInputs() []input.Input
		GetOutput() *output.Output
		GetDependencies() []ID
	}

	Details struct {
		Type           Type   `json:"type"`
		ID             ID     `json:"id"`
		SuccessStatus  Status `json:"success_status"`
		TimeLimit      int    `json:"time_limit"`
		MemoryLimit    int    `json:"memory_limit"`
		ExpectedTime   int    `json:"expected_time"`
		ExpectedMemory int    `json:"expected_memory"`
	}

	Type   string
	Status string
)

const (
	CompileCpp Type = "compile_cpp"
	CompileGo  Type = "compile_go"
	RunCpp     Type = "run_cpp"
	RunPy      Type = "run_py"
	RunGo      Type = "run_go"
	CheckCpp   Type = "check_cpp"
	Chain      Type = "chain"

	StatusOK Status = "OK"
	StatusCE Status = "CE"
	StatusRE Status = "RE"
	StatusTL Status = "TL"
	StatusML Status = "ML"
	StatusWA Status = "WA"
)

func (jb *Details) GetType() Type {
	return jb.Type
}

func (jb *Details) GetID() ID {
	return jb.ID
}

func (jb *Details) GetSuccessStatus() Status {
	return jb.SuccessStatus
}

func (jb *Details) GetTimeLimit() int {
	return jb.TimeLimit
}

func (jb *Details) GetMemoryLimit() int {
	return jb.MemoryLimit
}

func (jb *Details) GetExpectedTime() int {
	return jb.ExpectedTime
}

func (jb *Details) GetExpectedMemory() int {
	return jb.ExpectedMemory
}
