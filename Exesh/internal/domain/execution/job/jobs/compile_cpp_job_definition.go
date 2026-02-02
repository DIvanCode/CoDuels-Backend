package jobs

import (
	"exesh/internal/domain/execution/input/inputs"
	"exesh/internal/domain/execution/job"
)

type CompileCppJobDefinition struct {
	job.DefinitionDetails
	Code inputs.Definition `json:"code"`
}
