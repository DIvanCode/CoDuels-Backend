package results

import (
	"encoding/json"
	"exesh/internal/domain/execution"
	"fmt"
)

func UnmarshalResultJSON(data []byte) (result execution.Result, err error) {
	var details execution.ResultDetails
	if err = json.Unmarshal(data, &details); err != nil {
		err = fmt.Errorf("failed to unmarshal details: %w", err)
		return
	}

	switch details.Type {
	case execution.CompileResult:
		result = &CompileResult{}
	case execution.RunResult:
		result = &RunResult{}
	case execution.CheckResult:
		result = &CheckResult{}
	default:
		err = fmt.Errorf("unknown result type: %s", details.Type)
		return
	}

	if err = json.Unmarshal(data, result); err != nil {
		err = fmt.Errorf("failed to unmarshal %s result: %w", details.Type, err)
		return
	}
	return
}

func UnmarshalResultsJSON(data []byte) (resultsArray []execution.Result, err error) {
	var array []json.RawMessage
	if err = json.Unmarshal(data, &array); err != nil {
		err = fmt.Errorf("failed to unmarshal array: %w", err)
		return
	}

	resultsArray = make([]execution.Result, 0, len(array))
	for _, item := range array {
		var result execution.Result
		result, err = UnmarshalResultJSON(item)
		if err != nil {
			err = fmt.Errorf("failed to unmarshal result: %w", err)
			return
		}
		resultsArray = append(resultsArray, result)
	}
	return
}
