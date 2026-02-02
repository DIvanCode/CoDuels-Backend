package results

import (
	"encoding/json"
	"exesh/internal/domain/execution/result"
	"fmt"
)

type Result struct {
	result.IResult
}

func (res Result) MarshalJSON() ([]byte, error) {
	if res.IResult == nil {
		return []byte("null"), nil
	}

	return json.Marshal(res.IResult)
}

func (res *Result) UnmarshalJSON(data []byte) error {
	var details result.Details
	if err := json.Unmarshal(data, &details); err != nil {
		return fmt.Errorf("failed to unmarshal result details: %w", err)
	}

	switch details.Type {
	case result.Compile:
		res.IResult = &CompileResult{}
	case result.Run:
		res.IResult = &RunResult{}
	case result.Check:
		res.IResult = &CheckResult{}
	default:
		return fmt.Errorf("unknown result type: %s", details.Type)
	}

	if err := json.Unmarshal(data, res.IResult); err != nil {
		return fmt.Errorf("failed to unmarshal %s result: %w", details.Type, err)
	}

	return nil
}

func (res *Result) AsCompile() *CompileResult {
	return res.IResult.(*CompileResult)
}

func (res *Result) AsRun() *RunResult {
	return res.IResult.(*RunResult)
}

func (res *Result) AsCheck() *CheckResult {
	return res.IResult.(*CheckResult)
}
