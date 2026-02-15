package tasks

import (
	"encoding/json"
	"fmt"
	"taski/internal/domain/task"
)

func UnmarshalTaskJSON(data []byte) (t task.Task, err error) {
	details := task.Details{}
	if err = json.Unmarshal(data, &details); err != nil {
		err = fmt.Errorf("failed to unmarshal task details: %w", err)
		return
	}

	switch details.Type {
	case task.WriteCode:
		t = &WriteCodeTask{}
	case task.FindTest:
		t = &FindTestTask{}
	case task.PredictOutput:
		t = &PredictOutputTask{}
	default:
		err = fmt.Errorf("unknown task type: %s", details.Type)
		return
	}

	if err = json.Unmarshal(data, t); err != nil {
		err = fmt.Errorf("failed to unmarshal %s task: %w", details.Type, err)
		return
	}

	return
}
