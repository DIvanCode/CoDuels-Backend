package job

type (
	IDefinition interface {
		GetType() Type
		GetName() DefinitionName
		GetSuccessStatus() Status
		GetCategoryName() string
		GetTimeLimit() int
		GetMemoryLimit() int
	}

	DefinitionDetails struct {
		Type          Type           `json:"type"`
		Name          DefinitionName `json:"name"`
		SuccessStatus Status         `json:"success_status"`
		CategoryName  string         `json:"category_name"`
		TimeLimit     int            `json:"time_limit"`
		MemoryLimit   int            `json:"memory_limit"`
	}

	DefinitionName string
)

func (def *DefinitionDetails) GetType() Type {
	return def.Type
}

func (def *DefinitionDetails) GetName() DefinitionName {
	return def.Name
}

func (def *DefinitionDetails) GetSuccessStatus() Status {
	return def.SuccessStatus
}

func (def *DefinitionDetails) GetCategoryName() string {
	return def.CategoryName
}

func (def *DefinitionDetails) GetTimeLimit() int {
	return def.TimeLimit
}

func (def *DefinitionDetails) GetMemoryLimit() int {
	return def.MemoryLimit
}
