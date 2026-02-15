package job

type (
	IJob interface {
		GetType() Type
		GetName() Name
		GetSuccessStatus() Status
	}

	Details struct {
		Type          Type   `json:"type"`
		Name          Name   `json:"name"`
		SuccessStatus Status `json:"success_status"`
	}

	Type   string
	Name   string
	Status string
)

const (
	CompileCpp Type = "compile_cpp"
	CompileGo  Type = "compile_go"
	RunCpp     Type = "run_cpp"
	RunPy      Type = "run_py"
	RunGo      Type = "run_go"
	CheckCpp   Type = "check_cpp"

	StatusOK Status = "OK"
	StatusCE Status = "CE"
	StatusRE Status = "RE"
	StatusTL Status = "TL"
	StatusML Status = "ML"
	StatusWA Status = "WA"
)

func (jb *Details) GetType() Type {
	return jb.Type
}

func (jb *Details) GetName() Name {
	return jb.Name
}

func (jb *Details) GetSuccessStatus() Status {
	return jb.SuccessStatus
}
