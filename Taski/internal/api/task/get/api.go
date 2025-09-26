package get

import (
	"taski/internal/api"
	"taski/internal/usecase/task/dto"
)

type GetTaskResponse struct {
	api.Response
	Task dto.TaskDto `json:"task,omitempty"`
}
