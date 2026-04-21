package results

import (
	"encoding/json"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result"
	"fmt"
)

type Result struct {
	result.IResult
}

func Error(jb jobs.Job, err error) Result {
	switch jb.GetType() {
	case job.CompileCpp, job.CompileGo:
		return NewCompileResultErr(jb.GetID(), err.Error(), 0, 0)
	case job.CheckCpp:
		return NewCheckResultErr(jb.GetID(), err.Error(), 0, 0)
	case job.RunCpp, job.RunGo, job.RunPy:
		return NewRunResultErr(jb.GetID(), err.Error(), 0, 0)
	case job.Chain:
		return NewChainResultErr(jb.GetID(), err.Error(), nil)
	default:
		return NewUnknownResultErr(jb.GetID(), err.Error())
	}
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
	case result.Chain:
		res.IResult = &ChainResult{}
	case result.Unknown:
		res.IResult = &UnknownResult{}
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

func (res *Result) AsUnknown() *UnknownResult {
	return res.IResult.(*UnknownResult)
}

func (res *Result) AsChain() *ChainResult {
	return res.IResult.(*ChainResult)
}
