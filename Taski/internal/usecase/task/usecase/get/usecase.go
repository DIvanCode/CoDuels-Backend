package get

import (
	"fmt"
	"log/slog"
	"taski/internal/domain/task"
	"taski/internal/domain/task/tasks"
	dto "taski/internal/usecase/task/dto"
)

type (
	Query struct {
		TaskID task.ID
	}

	UseCase struct {
		log     *slog.Logger
		storage taskStorage
	}

	taskStorage interface {
		Get(task.ID) (t task.Task, unlock func(), err error)
	}
)

func NewUseCase(log *slog.Logger, storage taskStorage) *UseCase {
	return &UseCase{
		log:     log,
		storage: storage,
	}
}

func (uc *UseCase) Get(query Query) (dto.TaskDto, error) {
	t, unlock, err := uc.storage.Get(query.TaskID)
	if err != nil {
		uc.log.Error("failed to get task from storage", slog.Any("err", err))
		return nil, fmt.Errorf("failed to get task from storage")
	}
	defer unlock()

	switch t.GetType() {
	case task.WriteCode:
		taskDto := &dto.WriteCodeTaskDto{}
		taskDto.SetDetails(t)

		typedTask := t.(*tasks.WriteCodeTask)
		taskDto.TimeLimit = typedTask.TimeLimit
		taskDto.MemoryLimit = typedTask.MemoryLimit
		taskDto.Tests = dto.ConvertTests(typedTask.Tests)

		return taskDto, nil
	case task.FixCode:
		taskDto := &dto.FixCodeTaskDto{}
		taskDto.SetDetails(t)

		typedTask := t.(*tasks.FixCodeTask)
		taskDto.Code = typedTask.Code.Path
		taskDto.TimeLimit = typedTask.TimeLimit
		taskDto.MemoryLimit = typedTask.MemoryLimit
		taskDto.Tests = dto.ConvertTests(typedTask.Tests)

		return taskDto, nil
	case task.AddCode:
		taskDto := &dto.AddCodeTaskDto{}
		taskDto.SetDetails(t)

		typedTask := t.(*tasks.AddCodeTask)
		taskDto.Code = typedTask.Code.Path
		taskDto.TimeLimit = typedTask.TimeLimit
		taskDto.MemoryLimit = typedTask.MemoryLimit
		taskDto.Tests = dto.ConvertTests(typedTask.Tests)

		return taskDto, nil
	case task.FindTest:
		taskDto := &dto.FindTestTaskDto{}
		taskDto.SetDetails(t)

		typedTask := t.(*tasks.FindTestTask)
		taskDto.Code = typedTask.Code.Path
		taskDto.TimeLimit = typedTask.TimeLimit
		taskDto.MemoryLimit = typedTask.MemoryLimit

		return taskDto, nil
	case task.PredictOutput:
		taskDto := &dto.PredictOutputTaskDto{}
		taskDto.SetDetails(t)

		typedTask := t.(*tasks.FixCodeTask)
		taskDto.Code = typedTask.Code.Path
		taskDto.Tests = dto.ConvertTests(typedTask.Tests)

		return taskDto, nil
	default:
		uc.log.Error("unknown task type", slog.Any("type", t.GetType()))
		return nil, fmt.Errorf("unknown task type %s", t.GetType())
	}
}
