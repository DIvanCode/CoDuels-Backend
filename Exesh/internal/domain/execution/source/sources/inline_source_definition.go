package sources

import (
	"exesh/internal/domain/execution/source"
)

type InlineSourceDefinition struct {
	source.DefinitionDetails
	Content string `json:"content"`
}
