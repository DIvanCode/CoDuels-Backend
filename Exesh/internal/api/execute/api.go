package execute

import (
	"encoding/json"
	"exesh/internal/api"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/steps"
)

type (
	Request struct {
		Steps []execution.Step `json:"steps"`
	}

	Response struct {
		api.Response
		ExecutionID *execution.ID `json:"execution_id,omitempty"`
	}
)

func (r *Request) UnmarshalJSON(data []byte) error {
	req := struct {
		Steps json.RawMessage `json:"steps"`
	}{}
	if err := json.Unmarshal(data, &req); err != nil {
		return err
	}
	var err error
	r.Steps, err = steps.UnmarshalStepsJSON(req.Steps)
	if err != nil {
		return err
	}
	return nil
}
