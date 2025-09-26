package task

type Type string

const (
	WriteCode     Type = "write_code"
	FixCode       Type = "fix_code"
	AddCode       Type = "add_code"
	FindTest      Type = "find_test"
	PredictOutput Type = "predict_output"
)
