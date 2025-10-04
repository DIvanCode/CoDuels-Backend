package steps

import "taski/internal/domain/testing"

type CompileCppStep struct {
	testing.StepDetails
	Code testing.Source `json:"code"`
}

func NewCompileCppStep(name string, code testing.Source) CompileCppStep {
	return CompileCppStep{
		StepDetails: testing.StepDetails{
			Name: name,
			Type: testing.CompileCpp,
		},
		Code: code,
	}
}
