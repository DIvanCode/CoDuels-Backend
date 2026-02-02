package job

type (
	IDefinition interface {
		GetType() Type
		GetName() DefinitionName
		GetSuccessStatus() Status
	}

	DefinitionDetails struct {
		Type          Type           `json:"type"`
		Name          DefinitionName `json:"name"`
		SuccessStatus Status         `json:"success_status"`
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
