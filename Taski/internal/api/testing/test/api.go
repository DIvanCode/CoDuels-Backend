package test

import (
	"taski/internal/domain/task"
	"taski/internal/domain/testing"
)

type TestRequest struct {
	TaskID             task.ID                    `json:"task_id"`
	Solution           string                     `json:"solution"`
	Lang               task.Language              `json:"language"`
}

type TestResponse struct {
	api.Response
	SolutionId string `json:"solution_id,omitempty"`
}
