package execution

type (
	Step interface {
		GetName() string
		GetType() StepType
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

func (s StepDetails) GetName() string {
	return s.Name
}

func (s StepDetails) GetType() StepType {
	return s.Type
}
