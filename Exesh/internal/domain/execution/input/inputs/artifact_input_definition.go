package inputs

import (
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
)

type ArtifactInputDefinition struct {
	input.DefinitionDetails
	JobDefinitionName job.DefinitionName `json:"job"`
}
