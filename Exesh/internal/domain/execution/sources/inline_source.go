package sources

import (
	"exesh/internal/domain/execution"
)

type InlineSource struct {
	execution.SourceDetails
	Content string `json:"content"`
}
