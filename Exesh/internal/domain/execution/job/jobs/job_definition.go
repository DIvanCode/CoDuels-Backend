package jobs

import (
	"encoding/json"
	"exesh/internal/domain/execution/job"
	"fmt"
)

type Definition struct {
	job.IDefinition
}

func (def Definition) MarshalJSON() ([]byte, error) {
	if def.IDefinition == nil {
		return []byte("null"), nil
	}

	return json.Marshal(def.IDefinition)
}

func (def *Definition) UnmarshalJSON(data []byte) error {
	var details job.DefinitionDetails
	if err := json.Unmarshal(data, &details); err != nil {
		return fmt.Errorf("failed to unmarshal job definition details: %w", err)
	}

	switch details.Type {
	case job.CompileCpp:
		def.IDefinition = &CompileCppJobDefinition{}
	case job.CompileGo:
		def.IDefinition = &CompileGoJobDefinition{}
	case job.RunCpp:
		def.IDefinition = &RunCppJobDefinition{}
	case job.RunGo:
		def.IDefinition = &RunGoJobDefinition{}
	case job.RunPy:
		def.IDefinition = &RunPyJobDefinition{}
	case job.CheckCpp:
		def.IDefinition = &CheckCppJobDefinition{}
	default:
		return fmt.Errorf("unknown job definition type: %s", details.Type)
	}

	if err := json.Unmarshal(data, def.IDefinition); err != nil {
		return fmt.Errorf("failed to unmarshal %s job definition: %w", details.Type, err)
	}

	return nil
}

func (def *Definition) AsCompileCpp() *CompileCppJobDefinition {
	return def.IDefinition.(*CompileCppJobDefinition)
}

func (def *Definition) AsCompileGo() *CompileGoJobDefinition {
	return def.IDefinition.(*CompileGoJobDefinition)
}

func (def *Definition) AsRunCpp() *RunCppJobDefinition {
	return def.IDefinition.(*RunCppJobDefinition)
}

func (def *Definition) AsRunGo() *RunGoJobDefinition {
	return def.IDefinition.(*RunGoJobDefinition)
}

func (def *Definition) AsRunPy() *RunPyJobDefinition {
	return def.IDefinition.(*RunPyJobDefinition)
}

func (def *Definition) AsCheckCpp() *CheckCppJobDefinition {
	return def.IDefinition.(*CheckCppJobDefinition)
}
