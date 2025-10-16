package test

import (
	"taski/internal/domain/task"
	"taski/internal/domain/testing"
)

type Request struct {
	TaskID     task.ID            `json:"task_id"`
	SolutionID testing.SolutionID `json:"solution_id"`
	Solution   string             `json:"solution"`
	Lang       task.Language      `json:"language"`
}
