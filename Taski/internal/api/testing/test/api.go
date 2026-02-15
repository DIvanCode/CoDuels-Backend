package test

import (
	"taski/internal/domain/task"
	"taski/internal/domain/testing"
)

type Request struct {
	ExternalSolutionID testing.ExternalSolutionID `json:"solution_id"`
	TaskID             task.ID                    `json:"task_id"`
	Solution           string                     `json:"solution"`
	Lang               task.Language              `json:"language"`
}
