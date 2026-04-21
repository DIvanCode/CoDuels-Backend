package jobs

import (
	"exesh/internal/domain/execution/input/inputs"
	"exesh/internal/domain/execution/job"
)

type RunCppJobDefinition struct {
	job.DefinitionDetails
	CompiledCode inputs.Definition `json:"compiled_code"`
	RunInput     inputs.Definition `json:"input"`
	ShowOutput   bool              `json:"show_output"`
}
