package factory

import (
	"context"
	"crypto/sha1"
	"encoding/json"
	"exesh/internal/config"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/inputs"
	"exesh/internal/domain/execution/jobs"
	"exesh/internal/domain/execution/outputs"
	"exesh/internal/domain/execution/sources"
	"exesh/internal/domain/execution/steps"
	"fmt"
	"io"
	"log/slog"

	"github.com/google/uuid"
)

type (
	JobFactory struct {
		log *slog.Logger
		cfg config.JobFactoryConfig

		artifactRegistry artifactRegistry
		inputProvider    inputProvider
	}

	artifactRegistry interface {
		GetWorker(execution.JobID) (string, error)
	}

	inputProvider interface {
		Create(context.Context, execution.Input) (w io.Writer, commit, abort func() error, err error)
		Locate(context.Context, execution.Input) (path string, unlock func(), err error)
		Read(context.Context, execution.Input) (r io.Reader, unlock func(), err error)
	}
)

func NewJobFactory(
	log *slog.Logger,
	cfg config.JobFactoryConfig,
	artifactRegistry artifactRegistry,
	inputProvider inputProvider,
) *JobFactory {
	return &JobFactory{
		log: log,
		cfg: cfg,

		artifactRegistry: artifactRegistry,
		inputProvider:    inputProvider,
	}
}

func (f *JobFactory) Create(ctx context.Context, execCtx *execution.Context, step execution.Step) (execution.Job, error) {
	switch step.GetType() {
	case execution.CompileCppStepType:
		typedStep := step.(*steps.CompileCppStep)

		code, err := f.createInput(ctx, execCtx, typedStep.Code)
		if err != nil {
			return nil, fmt.Errorf("failed to create code source: %w", err)
		}

		id, err := f.calculateID(ctx, []execution.Input{code}, map[string]any{})
		if err != nil {
			return nil, fmt.Errorf("failed to calculate job id for step '%s': %w", step.GetName(), err)
		}

		compiledCode := outputs.NewArtifactOutput(f.cfg.Output.CompiledCpp, id)

		return jobs.NewCompileCppJob(id, code, compiledCode), nil
	case execution.RunCppStepType:
		typedStep := step.(*steps.RunCppStep)

		compiledCode, err := f.createInput(ctx, execCtx, typedStep.CompiledCode)
		if err != nil {
			return nil, fmt.Errorf("failed to create compiled_code source: %w", err)
		}
		runSource, err := f.createInput(ctx, execCtx, typedStep.RunInput)
		if err != nil {
			return nil, fmt.Errorf("failed to create run_input source: %w", err)
		}

		id, err := f.calculateID(ctx, []execution.Input{compiledCode, runSource}, step.GetAttributes())
		if err != nil {
			return nil, fmt.Errorf("failed to calculate job id for step '%s': %w", step.GetName(), err)
		}

		runOutput := outputs.NewArtifactOutput(f.cfg.Output.RunOutput, id)

		return jobs.NewRunCppJob(id, compiledCode, runSource, runOutput, typedStep.TimeLimit, typedStep.MemoryLimit, typedStep.ShowOutput), nil
	case execution.RunPyStepType:
		typedStep := step.(*steps.RunPyStep)

		code, err := f.createInput(ctx, execCtx, typedStep.Code)
		if err != nil {
			return nil, fmt.Errorf("failed to create code source: %w", err)
		}
		runSource, err := f.createInput(ctx, execCtx, typedStep.RunInput)
		if err != nil {
			return nil, fmt.Errorf("failed to create run_input source: %w", err)
		}

		id, err := f.calculateID(ctx, []execution.Input{code, runSource}, step.GetAttributes())
		if err != nil {
			return nil, fmt.Errorf("failed to calculate job id for step '%s': %w", step.GetName(), err)
		}

		runOutput := outputs.NewArtifactOutput(f.cfg.Output.RunOutput, id)

		return jobs.NewRunPyJob(id, code, runSource, runOutput, typedStep.TimeLimit, typedStep.MemoryLimit, typedStep.ShowOutput), nil
	case execution.CompileGoStepType:
		typedStep := step.(*steps.CompileGoStep)

		code, err := f.createInput(ctx, execCtx, typedStep.Code)
		if err != nil {
			return nil, fmt.Errorf("failed to create code source: %w", err)
		}

		id, err := f.calculateID(ctx, []execution.Input{code}, map[string]any{})
		if err != nil {
			return nil, fmt.Errorf("failed to calculate job id for step '%s': %w", step.GetName(), err)
		}

		compiledCode := outputs.NewArtifactOutput(f.cfg.Output.CompiledCpp, id)

		return jobs.NewCompileGoJob(id, code, compiledCode), nil
	case execution.RunGoStepType:
		typedStep := step.(*steps.RunGoStep)

		code, err := f.createInput(ctx, execCtx, typedStep.CompiledCode)
		if err != nil {
			return nil, fmt.Errorf("failed to create code source: %w", err)
		}
		runSource, err := f.createInput(ctx, execCtx, typedStep.RunInput)
		if err != nil {
			return nil, fmt.Errorf("failed to create run_input source: %w", err)
		}

		id, err := f.calculateID(ctx, []execution.Input{code, runSource}, step.GetAttributes())
		if err != nil {
			return nil, fmt.Errorf("failed to calculate job id for step '%s': %w", step.GetName(), err)
		}

		runOutput := outputs.NewArtifactOutput(f.cfg.Output.RunOutput, id)

		return jobs.NewRunGoJob(id, code, runSource, runOutput, typedStep.TimeLimit, typedStep.MemoryLimit, typedStep.ShowOutput), nil
	case execution.CheckCppStepType:
		typedStep := step.(*steps.CheckCppStep)

		compiledChecker, err := f.createInput(ctx, execCtx, typedStep.CompiledChecker)
		if err != nil {
			return nil, fmt.Errorf("failed to create compiled_checker source: %w", err)
		}
		correctOutput, err := f.createInput(ctx, execCtx, typedStep.CorrectOutput)
		if err != nil {
			return nil, fmt.Errorf("failed to create correct_output source: %w", err)
		}
		suspectOutput, err := f.createInput(ctx, execCtx, typedStep.SuspectOutput)
		if err != nil {
			return nil, fmt.Errorf("failed to create suspect_output source: %w", err)
		}

		id, err := f.calculateID(ctx, []execution.Input{compiledChecker, correctOutput, suspectOutput}, step.GetAttributes())
		if err != nil {
			return nil, fmt.Errorf("failed to calculate job id for step '%s': %w", step.GetName(), err)
		}

		return jobs.NewCheckCppJob(id, compiledChecker, correctOutput, suspectOutput), nil
	default:
		return nil, fmt.Errorf("unknown step type %s", step.GetType())
	}
}

func (f *JobFactory) createInput(ctx context.Context, execCtx *execution.Context, source execution.Source) (input execution.Input, err error) {
	switch source.GetType() {
	case execution.OtherStepSourceType:
		typedSource := source.(*sources.OtherStepSource)
		otherJob, ok := execCtx.GetJobForStep(typedSource.StepName)
		if !ok {
			return nil, fmt.Errorf("failed to get job id for step %s", typedSource.StepName)
		}
		otherJobOutput := otherJob.GetOutput()
		if otherJobOutput == nil {
			return nil, fmt.Errorf("failed to get dep job output for step %s", typedSource.StepName)
		}
		workerID, err := f.artifactRegistry.GetWorker(otherJob.GetID())
		if err != nil {
			return nil, fmt.Errorf("failed to find worker for job %s: %w", otherJob.GetID().String(), err)
		}
		input = inputs.NewArtifactInput(otherJobOutput.GetFile(), otherJob.GetID(), workerID)
	case execution.InlineSourceType:
		typedSource := source.(*sources.InlineSource)
		input = inputs.NewFilestorageBucketInput(uuid.New().String(), execCtx.InlineSourcesBucketID, f.cfg.FilestorageEndpoint)
		w, commit, abort, err := f.inputProvider.Create(ctx, input)
		if err != nil {
			return nil, fmt.Errorf("failed to save inline source to filestorage: %w", err)
		}
		if _, err = w.Write([]byte(typedSource.Content)); err != nil {
			_ = abort()
			return nil, fmt.Errorf("failed to write inline source to filestorage: %w", err)
		}
		if err = commit(); err != nil {
			_ = abort()
			return nil, fmt.Errorf("failed to commit filestorage input creation: %w", err)
		}
	case execution.FilestorageBucketSourceType:
		typedSource := source.(*sources.FilestorageBucketSource)
		input = inputs.NewFilestorageBucketInput(typedSource.File, typedSource.BucketID, typedSource.DownloadEndpoint)
		_, unlock, err := f.inputProvider.Locate(ctx, input)
		if err != nil {
			return nil, fmt.Errorf("failed to locate filestorage bucket input: %w", err)
		}
		unlock()
		input = inputs.NewFilestorageBucketInput(typedSource.File, typedSource.BucketID, f.cfg.FilestorageEndpoint)
	default:
		err = fmt.Errorf("unknown source type %s: %w", source.GetType(), err)
	}
	return input, err
}

func (f *JobFactory) calculateID(
	ctx context.Context,
	inputs []execution.Input,
	attributes map[string]any,
) (id execution.JobID, err error) {
	hash := sha1.New()
	for _, input := range inputs {
		var r io.Reader
		var unlock func()
		r, unlock, err = f.inputProvider.Read(ctx, input)
		if err != nil {
			err = fmt.Errorf("failed to read %s input: %w", input.GetType(), err)
			return id, err
		}
		var content []byte
		if content, err = io.ReadAll(r); err != nil {
			unlock()
			err = fmt.Errorf("failed to read %s input's content: %w", input.GetType(), err)
			return id, err
		}
		unlock()
		if _, err = hash.Write(content); err != nil {
			err = fmt.Errorf("failed to write %s input to hash: %w", input.GetType(), err)
			return id, err
		}
	}

	bytes, err := json.Marshal(attributes)
	if err != nil {
		err = fmt.Errorf("failed to marshal attributes: %w", err)
		return id, err
	}
	hash.Write(bytes)

	// temp: add random string to make all ids different
	rand, err := uuid.NewUUID()
	if err != nil {
		err = fmt.Errorf("failed to generate random string")
		return id, err
	}
	hash.Write([]byte(rand.String()))

	if err = id.FromString(fmt.Sprintf("%x", hash.Sum(nil))); err != nil {
		return id, err
	}

	return id, err
}
