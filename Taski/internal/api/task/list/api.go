package list

import (
	"taski/internal/api"
	"taski/internal/usecase/task/dto"
)

type TaskListResponse struct {
	api.Response
	Tasks []dto.TaskDto `json:"tasks,omitempty"`
}
