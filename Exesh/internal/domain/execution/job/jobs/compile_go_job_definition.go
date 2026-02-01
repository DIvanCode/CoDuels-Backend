package jobs

import (
	"exesh/internal/domain/execution/input/inputs"
	"exesh/internal/domain/execution/job"
)

type CompileGoJobDefinition struct {
	job.DefinitionDetails
	Code inputs.Definition `json:"code"`
}
