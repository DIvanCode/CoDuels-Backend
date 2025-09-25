package sources

import (
	"exesh/internal/domain/execution"
)

type InputSource struct {
	execution.SourceDetails
	Name    string `json:"name"`
	Content string `json:"content"`
	File    string `json:"file"`
}
