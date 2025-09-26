package tasks

import "taski/internal/domain/task"

type PredictOutputTask struct {
	task.Details
	Code  task.Code   `json:"code"`
	Tests []task.Test `json:"tests"`
}
