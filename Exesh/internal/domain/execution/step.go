package execution

type (
	Step interface {
		GetName() StepName
		GetType() StepType
		GetSources() []Source
		GetDependencies() []StepName
		GetAttributes() map[string]any
	}

	StepDetails struct {
		Name StepName `json:"name"`
		Type StepType `json:"type"`
	}

	StepName string

	StepType string
)

const (
	CompileCppStepType StepType = "compile_cpp"
	RunCppStepType     StepType = "run_cpp"
	RunPyStepType      StepType = "run_py"
	RunGoStepType      StepType = "run_go"
	CheckCppStepType   StepType = "check_cpp"
)

func (step StepDetails) GetName() StepName {
	return step.Name
}

func (step StepDetails) GetType() StepType {
	return step.Type
}
