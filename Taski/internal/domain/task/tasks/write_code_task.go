package tasks

import "taski/internal/domain/task"

type WriteCodeTask struct {
	task.Details
	SourceCode  *task.Code  `json:"source_code,omitempty"`
	TimeLimit   int         `json:"tl"`
	MemoryLimit int         `json:"ml"`
	Checker     task.Code   `json:"checker"`
	Solution    task.Code   `json:"solution"`
	Tests       []task.Test `json:"tests"`
}
