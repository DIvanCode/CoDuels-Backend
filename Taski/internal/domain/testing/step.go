package testing

type (
	Step interface {
		GetType() StepType
	}

	StepDetails struct {
		Name string   `json:"name"`
		Type StepType `json:"type"`
	}

	StepType string
)

const (
	CompileCpp StepType = "compile_cpp"
	CompileGo  StepType = "compile_go"
	RunCpp     StepType = "run_cpp"
	RunPy      StepType = "run_py"
	RunGo      StepType = "run_go"
	CheckCpp   StepType = "check_cpp"
)

func (s StepDetails) GetType() StepType {
	return s.Type
}
