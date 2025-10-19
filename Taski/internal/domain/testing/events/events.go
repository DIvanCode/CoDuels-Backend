package events

import (
	"encoding/json"
	"fmt"
	"taski/internal/domain/testing"
)

func UnmarshalEventJSON(data []byte) (event testing.Event, err error) {
	var details testing.EventDetails
	if err = json.Unmarshal(data, &details); err != nil {
		err = fmt.Errorf("failed to unmarshal details: %w", err)
		return
	}

	switch details.Type {
	case testing.StartExecutionEvent:
		event = &StartExecutionEvent{}
	case testing.CompileStepEvent:
		event = &CompileStepEvent{}
	case testing.RunStepEvent:
		event = &RunStepEvent{}
	case testing.CheckStepEvent:
		event = &CheckStepEvent{}
	case testing.FinishExecutionEvent:
		event = &FinishExecutionEvent{}
	default:
		err = fmt.Errorf("unknown event type: %s", details.Type)
		return
	}

	if err = json.Unmarshal(data, event); err != nil {
		err = fmt.Errorf("failed to unmarshal %s event: %w", details.Type, err)
		return
	}
	return
}
