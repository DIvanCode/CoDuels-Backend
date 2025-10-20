package dto

import "taski/internal/domain/task"

type TaskDto interface {
	SetDetails(t task.Task)
}

type taskDetailsDto struct {
	ID        task.ID   `json:"id"`
	Title     string    `json:"title"`
	Type      task.Type `json:"type"`
	Statement string    `json:"statement"`
}

func (d *taskDetailsDto) SetDetails(t task.Task) {
	d.ID = t.GetID()
	d.Title = t.GetTitle()
	d.Type = t.GetType()
	d.Statement = t.GetStatement()
}

type WriteCodeTaskDto struct {
	taskDetailsDto
	TimeLimit   int       `json:"tl"`
	MemoryLimit int       `json:"ml"`
	Tests       []TestDto `json:"tests"`
}

type FixCodeTaskDto struct {
	taskDetailsDto
	Code        string    `json:"code"`
	TimeLimit   int       `json:"tl"`
	MemoryLimit int       `json:"ml"`
	Tests       []TestDto `json:"tests"`
}

type AddCodeTaskDto struct {
	taskDetailsDto
	Code        string    `json:"code"`
	TimeLimit   int       `json:"tl"`
	MemoryLimit int       `json:"ml"`
	Tests       []TestDto `json:"tests"`
}

type FindTestTaskDto struct {
	taskDetailsDto
	Code        string `json:"code"`
	TimeLimit   int    `json:"tl"`
	MemoryLimit int    `json:"ml"`
}

type PredictOutputTaskDto struct {
	taskDetailsDto
	Code  string    `json:"code"`
	Tests []TestDto `json:"tests"`
}

type TestDto struct {
	Order  int    `json:"order"`
	Input  string `json:"input"`
	Output string `json:"output"`
}

func ConvertTests(tests []task.Test) []TestDto {
	testsDto := make([]TestDto, 0, len(tests))
	for _, test := range tests {
		if !test.Visible {
			continue
		}

		testsDto = append(testsDto, TestDto{
			Order:  test.ID,
			Input:  test.Input,
			Output: test.Output,
		})
	}
	return testsDto
}
