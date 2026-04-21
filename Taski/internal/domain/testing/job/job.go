package job

type (
	IJob interface {
		GetType() Type
		GetName() Name
		GetSuccessStatus() Status
		GetCategoryName() string
		GetTimeLimit() int
		GetMemoryLimit() int
	}

	Details struct {
		Type          Type   `json:"type"`
		Name          Name   `json:"name"`
		SuccessStatus Status `json:"success_status"`
		CategoryName  string `json:"category_name"`
		TimeLimit     int    `json:"time_limit"`
		MemoryLimit   int    `json:"memory_limit"`
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

func (jb *Details) GetCategoryName() string {
	return jb.CategoryName
}

func (jb *Details) GetTimeLimit() int {
	return jb.TimeLimit
}

func (jb *Details) GetMemoryLimit() int {
	return jb.MemoryLimit
}
