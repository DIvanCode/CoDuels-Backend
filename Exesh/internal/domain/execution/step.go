package execution

type (
	Step interface {
		GetName() string
		GetType() StepType
		GetAttributes() map[string]any
	}

	StepDetails struct {
		Name string   `json:"name"`
		Type StepType `json:"type"`
	}

	StepType string
)

const (
	CompileCppStepType StepType = "compile_cpp"
	RunCppStepType     StepType = "run_cpp"
	RunPyStepType      StepType = "run_py"
	RunGoStepType      StepType = "run_go"
	CheckCppStepType   StepType = "check_cpp"
)

func (step StepDetails) GetName() string {
	return step.Name
}

func (step StepDetails) GetType() StepType {
	return step.Type
}
