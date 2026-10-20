package factory

import (
	"context"
	"exesh/internal/config"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/input/inputs"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/source"
	"exesh/internal/domain/execution/source/sources"
	"testing"
)

type fixedCalculator struct{}

func (fixedCalculator) LoadCategoryStats(context.Context, execution.StageDefinitions) (execution.CategoryStats, error) {
	return execution.NewCategoryStats(), nil
}

func (fixedCalculator) EstimateForJob(jobs.Definition, execution.CategoryStats) (int, int) {
	return 100, 64
}

func TestReplayUsesFreshJobAndArtifactIDs(t *testing.T) {
	code := inputs.Definition{IDefinition: &inputs.InlineInputDefinition{
		DefinitionDetails:    input.DefinitionDetails{Type: input.InlineDefinition},
		SourceDefinitionName: "code",
	}}
	compiled := inputs.Definition{IDefinition: &inputs.ArtifactInputDefinition{
		DefinitionDetails: input.DefinitionDetails{Type: input.ArtifactDefinition},
		JobDefinitionName: "compile",
	}}
	def := execution.NewExecutionDefinition(execution.StageDefinitions{{
		Name: "test",
		Jobs: []jobs.Definition{
			{IDefinition: &jobs.CompileCppJobDefinition{
				DefinitionDetails: job.DefinitionDetails{Type: job.CompileCpp, Name: "compile", SuccessStatus: job.StatusOK},
				Code:              code,
			}},
			{IDefinition: &jobs.RunCppJobDefinition{
				DefinitionDetails: job.DefinitionDetails{Type: job.RunCpp, Name: "run", SuccessStatus: job.StatusOK},
				CompiledCode:      compiled,
				RunInput:          code,
			}},
		},
	}}, sources.Definitions{{IDefinition: &sources.InlineSourceDefinition{
		DefinitionDetails: source.DefinitionDetails{Type: source.InlineDefinition, Name: "code"},
		Content:           "int main() {}",
	}}}, 1)

	factory := NewExecutionFactory(config.JobFactoryConfig{}, nil, fixedCalculator{})
	first, err := factory.Create(context.Background(), def)
	if err != nil {
		t.Fatal(err)
	}
	second, err := factory.Create(context.Background(), def)
	if err != nil {
		t.Fatal(err)
	}

	firstCompileID := first.JobByName["compile"].GetID()
	secondCompileID := second.JobByName["compile"].GetID()
	if firstCompileID == secondCompileID {
		t.Fatal("replayed compile job reused the previous attempt's artifact bucket")
	}
	if first.JobByName["run"].GetID() == second.JobByName["run"].GetID() {
		t.Fatal("replayed run job reused the previous attempt's artifact bucket")
	}
	for _, tc := range []struct {
		name string
		ex   *execution.Execution
	}{
		{name: "first", ex: first},
		{name: "second", ex: second},
	} {
		t.Run(tc.name, func(t *testing.T) {
			compileID := tc.ex.JobByName["compile"].GetID()
			run := tc.ex.JobByName["run"]
			if deps := run.GetDependencies(); len(deps) != 1 || deps[0] != compileID {
				t.Fatalf("run dependencies = %v, want compile job %s", deps, compileID.String())
			}
			if _, ok := tc.ex.OutputByJob[compileID]; !ok {
				t.Fatal("compile output is not indexed by its attempt-specific job ID")
			}
		})
	}
}
