package task

type (
	Code struct {
		Path string   `json:"path"`
		Lang Language `json:"lang"`
	}

	Language string
)

const (
	LanguageCpp    Language = "Cpp"
	LanguagePython Language = "Python"
	LanguageGo     Language = "Golang"
)
