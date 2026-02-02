package jobs

import (
	"exesh/internal/domain/execution/input/inputs"
	"exesh/internal/domain/execution/job"
)

type RunGoJobDefinition struct {
	job.DefinitionDetails
	CompiledCode inputs.Definition `json:"compiled_code"`
	RunInput     inputs.Definition `json:"input"`
	TimeLimit    int               `json:"time_limit"`
	MemoryLimit  int               `json:"memory_limit"`
	ShowOutput   bool              `json:"show_output"`
}
