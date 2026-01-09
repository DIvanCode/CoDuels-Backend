package dto

import (
	"fmt"
	"taski/internal/domain/task"
	"taski/internal/domain/task/tasks"
)

type TaskDto interface {
	setDetails(t task.Task)
}

type taskDetailsDto struct {
	ID        task.ID    `json:"id"`
	Title     string     `json:"title"`
	Type      task.Type  `json:"type"`
	Level     task.Level `json:"level"`
	Topics    []string   `json:"topics"`
	Statement string     `json:"statement"`
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

func (d *taskDetailsDto) setDetails(t task.Task) {
	d.ID = t.GetID()
	d.Title = t.GetTitle()
	d.Type = t.GetType()
	d.Level = t.GetLevel()
	d.Topics = t.GetTopics()
	d.Statement = t.GetStatement()
}

func convertTests(tests []task.Test) []TestDto {
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

func ConvertTask(t task.Task) (TaskDto, error) {
	switch t.GetType() {
	case task.WriteCode:
		taskDto := &WriteCodeTaskDto{}
		taskDto.setDetails(t)

		typedTask := t.(*tasks.WriteCodeTask)
		taskDto.TimeLimit = typedTask.TimeLimit
		taskDto.MemoryLimit = typedTask.MemoryLimit
		taskDto.Tests = convertTests(typedTask.Tests)

		return taskDto, nil
	case task.FixCode:
		taskDto := &FixCodeTaskDto{}
		taskDto.setDetails(t)

		typedTask := t.(*tasks.FixCodeTask)
		taskDto.Code = typedTask.Code.Path
		taskDto.TimeLimit = typedTask.TimeLimit
		taskDto.MemoryLimit = typedTask.MemoryLimit
		taskDto.Tests = convertTests(typedTask.Tests)

		return taskDto, nil
	case task.AddCode:
		taskDto := &AddCodeTaskDto{}
		taskDto.setDetails(t)

		typedTask := t.(*tasks.AddCodeTask)
		taskDto.Code = typedTask.Code.Path
		taskDto.TimeLimit = typedTask.TimeLimit
		taskDto.MemoryLimit = typedTask.MemoryLimit
		taskDto.Tests = convertTests(typedTask.Tests)

		return taskDto, nil
	case task.FindTest:
		taskDto := &FindTestTaskDto{}
		taskDto.setDetails(t)

		typedTask := t.(*tasks.FindTestTask)
		taskDto.Code = typedTask.Code.Path
		taskDto.TimeLimit = typedTask.TimeLimit
		taskDto.MemoryLimit = typedTask.MemoryLimit

		return taskDto, nil
	case task.PredictOutput:
		taskDto := &PredictOutputTaskDto{}
		taskDto.setDetails(t)

		typedTask := t.(*tasks.FixCodeTask)
		taskDto.Code = typedTask.Code.Path
		taskDto.Tests = convertTests(typedTask.Tests)

		return taskDto, nil
	default:
		return nil, fmt.Errorf("unknown task type %s", t.GetType())
	}
}
