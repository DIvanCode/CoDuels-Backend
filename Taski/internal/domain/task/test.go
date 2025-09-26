package task

type Test struct {
	ID      int    `json:"id"`
	Input   string `json:"input"`
	Output  string `json:"output"`
	Visible bool   `json:"visible"`
}
