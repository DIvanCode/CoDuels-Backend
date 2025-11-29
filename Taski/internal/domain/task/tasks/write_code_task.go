package tasks

import "taski/internal/domain/task"

type WriteCodeTask struct {
	task.Details
	TimeLimit   int       `json:"tl"`
	MemoryLimit int       `json:"ml"`
	Checker     task.Code `json:"checker"`
	Solution    task.Code `json:"solution"`
}
