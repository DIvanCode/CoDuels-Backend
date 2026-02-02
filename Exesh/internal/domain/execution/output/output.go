package output

type Output struct {
	File string `json:"file"`
}

func NewOutput(file string) Output {
	return Output{
		File: file,
	}
}
