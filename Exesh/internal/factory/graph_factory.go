package factory

import (
	"context"
	"crypto/sha1"
	"encoding/json"
	"exesh/internal/config"
	"exesh/internal/domain/execution"
	executionInputs "exesh/internal/domain/execution/inputs"
	"exesh/internal/domain/execution/steps"
	"exesh/internal/domain/graph"
	graphInputs "exesh/internal/domain/graph/inputs"
	"exesh/internal/domain/graph/jobs"
	"exesh/internal/domain/graph/outputs"
	"fmt"
	"io"
	"log/slog"
)

type (
	GraphFactory struct {
		log *slog.Logger
		cfg config.GraphFactoryConfig

		inputProvider inputProvider
	}

	inputProvider interface {
		Get(ctx context.Context, input graph.Input) (r io.Reader, unlock func(), err error)
	}
)

func NewGraphFactory(log *slog.Logger, cfg config.GraphFactoryConfig, inputProvider inputProvider) *GraphFactory {
	return &GraphFactory{
		log: log,
		cfg: cfg,

		inputProvider: inputProvider,
	}
}

func (f *GraphFactory) CreateForExecution(ctx context.Context, e execution.Execution) (*graph.Graph, error) {
	jobs := make([]graph.Job, 0, len(e.Steps))
	jobByStep := make(map[string]graph.Job, len(e.Steps))

	for _, step := range e.Steps {
		job, err := f.convertJob(ctx, step, jobByStep)
		if err != nil {
			return nil, fmt.Errorf("failed to convert %s step '%s': %w", step.GetType(), step.GetName(), err)
		}
		jobByStep[step.GetName()] = job
		jobs = append(jobs, job)
	}

	return graph.NewGraph(jobs), nil
}

func (f *GraphFactory) convertJob(ctx context.Context, step execution.Step, jobByStep map[string]graph.Job) (graph.Job, error) {
	switch step.GetType() {
	case execution.CompileCppStepType:
		typedStep := step.(*steps.CompileCppStep)

		code, err := f.convertInput(typedStep.Code, jobByStep)
		if err != nil {
			return nil, fmt.Errorf("failed to convert code input: %w", err)
		}

		id, err := f.calculateID(ctx, []graph.Input{code}, map[string]any{})
		if err != nil {
			return nil, fmt.Errorf("failed to calculate job id for step '%s': %w", step.GetName(), err)
		}

		compiledCode := outputs.NewArtifactOutput(id, f.cfg.Output.CompiledCpp)

		return jobs.NewCompileCppJob(id, code, compiledCode), nil
	case execution.RunCppStepType:
		typedStep := step.(*steps.RunCppStep)

		compiledCode, err := f.convertInput(typedStep.CompiledCode, jobByStep)
		if err != nil {
			return nil, fmt.Errorf("failed to convert compiled_code input: %w", err)
		}
		runInput, err := f.convertInput(typedStep.RunInput, jobByStep)
		if err != nil {
			return nil, fmt.Errorf("failed to convert run_input input: %w", err)
		}

		id, err := f.calculateID(ctx, []graph.Input{compiledCode, runInput}, step.GetAttributes())
		if err != nil {
			return nil, fmt.Errorf("failed to calculate job id for step '%s': %w", step.GetName(), err)
		}

		runOutput := outputs.NewArtifactOutput(id, f.cfg.Output.RunOutput)

		return jobs.NewRunCppJob(id, compiledCode, runInput, runOutput, typedStep.TimeLimit, typedStep.MemoryLimit, typedStep.ShowOutput), nil
	case execution.RunPyStepType:
		typedStep := step.(*steps.RunPyStep)

		code, err := f.convertInput(typedStep.Code, jobByStep)
		if err != nil {
			return nil, fmt.Errorf("failed to convert code input: %w", err)
		}
		runInput, err := f.convertInput(typedStep.RunInput, jobByStep)
		if err != nil {
			return nil, fmt.Errorf("failed to convert run_input input: %w", err)
		}

		id, err := f.calculateID(ctx, []graph.Input{code, runInput}, step.GetAttributes())
		if err != nil {
			return nil, fmt.Errorf("failed to calculate job id for step '%s': %w", step.GetName(), err)
		}

		runOutput := outputs.NewArtifactOutput(id, f.cfg.Output.RunOutput)

		return jobs.NewRunPyJob(id, code, runInput, runOutput, typedStep.TimeLimit, typedStep.MemoryLimit, typedStep.ShowOutput), nil
	case execution.RunGoStepType:
		typedStep := step.(*steps.RunGoStep)

		code, err := f.convertInput(typedStep.Code, jobByStep)
		if err != nil {
			return nil, fmt.Errorf("failed to convert code input: %w", err)
		}
		runInput, err := f.convertInput(typedStep.RunInput, jobByStep)
		if err != nil {
			return nil, fmt.Errorf("failed to convert run_input input: %w", err)
		}

		id, err := f.calculateID(ctx, []graph.Input{code, runInput}, step.GetAttributes())
		if err != nil {
			return nil, fmt.Errorf("failed to calculate job id for step '%s': %w", step.GetName(), err)
		}

		runOutput := outputs.NewArtifactOutput(id, f.cfg.Output.RunOutput)

		return jobs.NewRunGoJob(id, code, runInput, runOutput, typedStep.TimeLimit, typedStep.MemoryLimit, typedStep.ShowOutput), nil
	case execution.CheckCppStepType:
		typedStep := step.(*steps.CheckCppStep)

		compiledChecker, err := f.convertInput(typedStep.CompiledChecker, jobByStep)
		if err != nil {
			return nil, fmt.Errorf("failed to convert compiled_checker input: %w", err)
		}
		correctOutput, err := f.convertInput(typedStep.CorrectOutput, jobByStep)
		if err != nil {
			return nil, fmt.Errorf("failed to convert correct_output input: %w", err)
		}
		suspectOutput, err := f.convertInput(typedStep.SuspectOutput, jobByStep)
		if err != nil {
			return nil, fmt.Errorf("failed to convert suspect_output input: %w", err)
		}

		id, err := f.calculateID(ctx, []graph.Input{compiledChecker, correctOutput, suspectOutput}, step.GetAttributes())
		if err != nil {
			return nil, fmt.Errorf("failed to calculate job id for step '%s': %w", step.GetName(), err)
		}

		checkVerdict := outputs.NewArtifactOutput(id, f.cfg.Output.CheckVerdict)

		return jobs.NewCheckCppJob(id, compiledChecker, correctOutput, suspectOutput, checkVerdict), nil
	default:
		return nil, fmt.Errorf("unknown step type %s", step.GetType())
	}
}

func (f *GraphFactory) convertInput(input execution.Input, jobByStep map[string]graph.Job) (graph.Input, error) {
	switch input.GetType() {
	case execution.InlineInputType:
		typedInput := input.(*executionInputs.InlineInput)
		return graphInputs.NewInlineInput(typedInput.Content), nil
	case execution.FilestorageBucketInputType:
		typedInput := input.(*executionInputs.FilestorageBucketInput)
		return graphInputs.NewFilestorageBucketInput(typedInput.BucketID, typedInput.DownloadEndpoint, typedInput.File), nil
	case execution.OtherStepInputType:
		typedInput := input.(*executionInputs.OtherStepInput)
		otherJob, ok := jobByStep[typedInput.StepName]
		if !ok {
			return nil, fmt.Errorf("failed to find job for step %s", typedInput.StepName)
		}
		return otherJob.GetOutput().ConvertToInput(), nil
	default:
		return nil, fmt.Errorf("unknown input type %s", input.GetType())
	}
}

func (f *GraphFactory) calculateID(ctx context.Context, inputs []graph.Input, attributes map[string]any) (id graph.JobID, err error) {
	hash := sha1.New()
	for _, input := range inputs {
		if input.GetType() == graph.ArtifactInputType {
			artifactInput := input.(graphInputs.ArtifactInput)
			attributes[artifactInput.JobID.String()] = artifactInput.File
			continue
		}

		var r io.Reader
		var unlock func()
		r, unlock, err = f.inputProvider.Get(ctx, input)
		if err != nil {
			err = fmt.Errorf("failed to get %s input: %w", input.GetType(), err)
			return
		}
		defer unlock()

		var content []byte
		content, err = io.ReadAll(r)
		if err != nil {
			err = fmt.Errorf("failed to read %s input: %w", input.GetType(), err)
			return
		}

		hash.Write(content)
	}

	bytes, err := json.Marshal(attributes)
	if err != nil {
		err = fmt.Errorf("failed to marshal attributes: %w", err)
		return
	}
	hash.Write(bytes)

	idStr := fmt.Sprintf("%x", hash.Sum(nil))
	if err = id.FromString(idStr); err != nil {
		return
	}

	return
}
