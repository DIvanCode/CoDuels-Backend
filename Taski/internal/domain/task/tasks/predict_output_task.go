package tasks

import "taski/internal/domain/task"

type PredictOutputTask struct {
	task.Details
	Code    task.Code `json:"code"`
	Checker task.Code `json:"checker"`
	Test    task.Test `json:"test"`
}
