package inputs

import (
	"exesh/internal/domain/execution"
)

type InlineInput struct {
	execution.InputDetails
	Content string `json:"content"`
}
