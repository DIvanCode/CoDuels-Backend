package jobs

import (
	"exesh/internal/domain/execution/input/inputs"
	"exesh/internal/domain/execution/job"
)

type RunPyJobDefinition struct {
	job.DefinitionDetails
	Code        inputs.Definition `json:"code"`
	RunInput    inputs.Definition `json:"input"`
	TimeLimit   int               `json:"time_limit"`
	MemoryLimit int               `json:"memory_limit"`
	ShowOutput  bool              `json:"show_output"`
}
