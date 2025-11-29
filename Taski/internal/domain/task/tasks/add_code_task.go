package tasks

import "taski/internal/domain/task"

type AddCodeTask struct {
	task.Details
	Code        task.Code `json:"code"`
	TimeLimit   int       `json:"tl"`
	MemoryLimit int       `json:"ml"`
	Checker     task.Code `json:"checker"`
	Solution    task.Code `json:"solution"`
}
