package steps

import "taski/internal/domain/testing"

type CompileGoStep struct {
	testing.StepDetails
	Code testing.Source `json:"code"`
}

func NewCompileGoStep(name string, code testing.Source) CompileGoStep {
	return CompileGoStep{
		StepDetails: testing.StepDetails{
			Name: name,
			Type: testing.CompileGo,
		},
		Code: code,
	}
}
