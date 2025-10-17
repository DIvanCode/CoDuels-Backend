package random

import (
	"taski/internal/api"
	"taski/internal/domain/task"
)

type RandomTaskResponse struct {
	api.Response
	TaskID task.ID `json:"task_id,omitempty"`
}
