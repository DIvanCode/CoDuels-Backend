package factory

import (
	"context"
	"crypto/sha1"
	"exesh/internal/config"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/input/inputs"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/output"
	"exesh/internal/domain/execution/source"
	"exesh/internal/domain/execution/source/sources"
	"fmt"
	"github.com/DIvanCode/filestorage/pkg/bucket"
	"time"
)

type (
	ExecutionFactory struct {
		cfg         config.JobFactoryConfig
		filestorage filestorage
	}

	filestorage interface {
		DownloadBucket(context.Context, bucket.ID, time.Duration, string) error
		DownloadFile(context.Context, bucket.ID, string, time.Duration, string) error
	}
)

func NewExecutionFactory(
	cfg config.JobFactoryConfig,
	filestorage filestorage,
) *ExecutionFactory {
	return &ExecutionFactory{
		cfg:         cfg,
		filestorage: filestorage,
	}
}

func (f *ExecutionFactory) Create(ctx context.Context, def execution.Definition) (*execution.Execution, error) {
	ex := execution.NewExecution(def)

	for _, srcDef := range def.Sources {
		if err := f.saveSource(ctx, ex, srcDef); err != nil {
			return nil, fmt.Errorf("failed to save source '%s': %w", srcDef.GetName(), err)
		}
	}

	for _, stageDef := range def.Stages {
		stage, err := f.createStage(ex, stageDef)
		if err != nil {
			return nil, fmt.Errorf("failed to create stage '%s': %w", stageDef.Name, err)
		}

		ex.Stages = append(ex.Stages, stage)
	}

	ex.BuildGraph()

	return ex, nil
}

func (f *ExecutionFactory) saveSource(ctx context.Context, ex *execution.Execution, def sources.Definition) error {
	switch def.GetType() {
	case source.InlineDefinition:
		break
	case source.FilestorageBucketDefinition:
		typedSrc := def.AsFilestorageBucketDefinition()

		bucketID := typedSrc.BucketID
		ttl := f.cfg.SourceTTL.FilestorageBucket
		downloadEndpoint := typedSrc.DownloadEndpoint

		if err := f.filestorage.DownloadBucket(ctx, bucketID, ttl, downloadEndpoint); err != nil {
			return fmt.Errorf("failed to download bucket %s: %w", bucketID, err)
		}
	case source.FilestorageBucketFileDefinition:
		typedSrc := def.AsFilestorageBucketFileDefinition()

		bucketID := typedSrc.BucketID
		file := typedSrc.File
		ttl := f.cfg.SourceTTL.FilestorageBucket
		downloadEndpoint := typedSrc.DownloadEndpoint

		if err := f.filestorage.DownloadFile(ctx, bucketID, file, ttl, downloadEndpoint); err != nil {
			return fmt.Errorf("failed to download file %s: %w", bucketID, err)
		}
	default:
		return fmt.Errorf("unknown source definition type '%s'", def.GetType())
	}

	ex.SourceDefinitionByName[def.GetName()] = def

	return nil
}

func (f *ExecutionFactory) createStage(ex *execution.Execution, def execution.StageDefinition) (*execution.Stage, error) {
	stage := execution.Stage{
		Name: def.Name,
		Deps: def.Deps,
		Jobs: make([]jobs.Job, 0, len(def.Jobs)),
	}

	for _, jobDef := range def.Jobs {
		jb, err := f.createJob(ex, jobDef)
		if err != nil {
			return nil, fmt.Errorf("failed to create job '%s': %w", jobDef.GetName(), err)
		}

		stage.Jobs = append(stage.Jobs, jb)
		ex.JobByName[jobDef.GetName()] = jb
	}

	return &stage, nil
}

func (f *ExecutionFactory) createJob(ex *execution.Execution, def jobs.Definition) (jobs.Job, error) {
	var jb jobs.Job

	id, err := f.calculateJobID(ex.ID.String(), string(def.GetName()))
	if err != nil {
		return jb, fmt.Errorf("failed to calculate job '%s' id: %w", def.GetName(), err)
	}

	successStatus := def.GetSuccessStatus()

	switch def.GetType() {
	case job.CompileCpp:
		typedDef := def.AsCompileCpp()

		code, err := f.createInput(ex, typedDef.Code)
		if err != nil {
			return jb, fmt.Errorf("failed to create code source: %w", err)
		}
		compiledCode := output.NewOutput(f.cfg.Output.CompiledBinary)

		jb = jobs.NewCompileCppJob(id, successStatus, code, compiledCode)
	case job.CompileGo:
		typedDef := def.AsCompileGo()

		code, err := f.createInput(ex, typedDef.Code)
		if err != nil {
			return jb, fmt.Errorf("failed to create code source: %w", err)
		}
		compiledCode := output.NewOutput(f.cfg.Output.CompiledBinary)

		jb = jobs.NewCompileGoJob(id, successStatus, code, compiledCode)
	case job.RunCpp:
		typedDef := def.AsRunCpp()

		compiledCode, err := f.createInput(ex, typedDef.CompiledCode)
		if err != nil {
			return jb, fmt.Errorf("failed to create compiled_code source: %w", err)
		}
		runInput, err := f.createInput(ex, typedDef.RunInput)
		if err != nil {
			return jb, fmt.Errorf("failed to create run_input source: %w", err)
		}
		runOutput := output.NewOutput(f.cfg.Output.RunOutput)
		timeLimit := typedDef.TimeLimit
		memoryLimit := typedDef.MemoryLimit
		showOutput := typedDef.ShowOutput

		jb = jobs.NewRunCppJob(id, successStatus, compiledCode, runInput, runOutput, timeLimit, memoryLimit, showOutput)
	case job.RunGo:
		typedDef := def.AsRunGo()

		compiledCode, err := f.createInput(ex, typedDef.CompiledCode)
		if err != nil {
			return jb, fmt.Errorf("failed to create compiled_code source: %w", err)
		}
		runInput, err := f.createInput(ex, typedDef.RunInput)
		if err != nil {
			return jb, fmt.Errorf("failed to create run_input source: %w", err)
		}
		runOutput := output.NewOutput(f.cfg.Output.RunOutput)
		timeLimit := typedDef.TimeLimit
		memoryLimit := typedDef.MemoryLimit
		showOutput := typedDef.ShowOutput

		jb = jobs.NewRunGoJob(id, successStatus, compiledCode, runInput, runOutput, timeLimit, memoryLimit, showOutput)
	case job.RunPy:
		typedDef := def.AsRunPy()

		code, err := f.createInput(ex, typedDef.Code)
		if err != nil {
			return jb, fmt.Errorf("failed to create code source: %w", err)
		}
		runInput, err := f.createInput(ex, typedDef.RunInput)
		if err != nil {
			return jb, fmt.Errorf("failed to create run_input source: %w", err)
		}
		runOutput := output.NewOutput(f.cfg.Output.RunOutput)
		timeLimit := typedDef.TimeLimit
		memoryLimit := typedDef.MemoryLimit
		showOutput := typedDef.ShowOutput

		jb = jobs.NewRunPyJob(id, successStatus, code, runInput, runOutput, timeLimit, memoryLimit, showOutput)
	case job.CheckCpp:
		typedDef := def.AsCheckCpp()

		compiledChecker, err := f.createInput(ex, typedDef.CompiledChecker)
		if err != nil {
			return jb, fmt.Errorf("failed to create compiled_checker source: %w", err)
		}
		correctOutput, err := f.createInput(ex, typedDef.CorrectOutput)
		if err != nil {
			return jb, fmt.Errorf("failed to create correct_output source: %w", err)
		}
		suspectOutput, err := f.createInput(ex, typedDef.SuspectOutput)
		if err != nil {
			return jb, fmt.Errorf("failed to create suspect_output source: %w", err)
		}

		jb = jobs.NewCheckCppJob(id, successStatus, compiledChecker, correctOutput, suspectOutput)
	default:
		return jb, fmt.Errorf("unknown job type %s", def.GetType())
	}

	ex.JobDefinitionByID[jb.GetID()] = def

	out := jb.GetOutput()
	if out != nil {
		ex.OutputByJob[jb.GetID()] = *out
	}

	return jb, nil
}

func (f *ExecutionFactory) createInput(ex *execution.Execution, def inputs.Definition) (input.Input, error) {
	var in input.Input

	switch def.GetType() {
	case input.InlineDefinition:
		typedDef := def.AsInline()

		srcDef, ok := ex.SourceDefinitionByName[typedDef.SourceDefinitionName]
		if !ok {
			return in, fmt.Errorf("failed to find source definition '%s'", typedDef.SourceDefinitionName)
		}
		typedSrcDef := srcDef.AsInlineDefinition()

		sourceID, err := f.calculateSourceID(ex.ID.String(), string(srcDef.GetName()))
		if err != nil {
			return in, fmt.Errorf("failed to calculate source id: %w", err)
		}

		src := sources.NewInlineSource(sourceID, typedSrcDef.Content)
		ex.SourceByID[src.GetID()] = src

		in = input.NewInput(input.Inline, src.GetID())
	case input.FilestorageBucketDefinition:
		typedDef := def.AsFilestorageBucket()

		srcDef, ok := ex.SourceDefinitionByName[typedDef.SourceDefinitionName]
		if !ok {
			return in, fmt.Errorf("failed to find source definition '%s'", typedDef.SourceDefinitionName)
		}
		typedSrcDef := srcDef.AsFilestorageBucketDefinition()

		sourceID, err := f.calculateSourceID(ex.ID.String(), string(srcDef.GetName()), typedDef.File)
		if err != nil {
			return in, fmt.Errorf("failed to calculate source id: %w", err)
		}

		bucketID := typedSrcDef.BucketID
		downloadEndpoint := f.cfg.FilestorageEndpoint
		file := typedDef.File

		src := sources.NewFilestorageBucketFileSource(sourceID, bucketID, downloadEndpoint, file)
		ex.SourceByID[src.GetID()] = src

		in = input.NewInput(input.FilestorageBucketFile, src.GetID())
	case input.FilestorageBucketFileDefinition:
		typedDef := def.AsFilestorageBucketFile()

		srcDef, ok := ex.SourceDefinitionByName[typedDef.SourceDefinitionName]
		if !ok {
			return in, fmt.Errorf("failed to find source definition '%s'", typedDef.SourceDefinitionName)
		}
		typedSrcDef := srcDef.AsFilestorageBucketFileDefinition()

		sourceID, err := f.calculateSourceID(ex.ID.String(), string(srcDef.GetName()))
		if err != nil {
			return in, fmt.Errorf("failed to calculate source id: %w", err)
		}

		bucketID := typedSrcDef.BucketID
		downloadEndpoint := f.cfg.FilestorageEndpoint
		file := typedSrcDef.File

		src := sources.NewFilestorageBucketFileSource(sourceID, bucketID, downloadEndpoint, file)
		ex.SourceByID[src.GetID()] = src

		in = input.NewInput(input.FilestorageBucketFile, src.GetID())
	case input.ArtifactDefinition:
		typedDef := def.AsArtifact()

		jb, ok := ex.JobByName[typedDef.JobDefinitionName]
		if !ok {
			return in, fmt.Errorf("failed to find job '%s'", typedDef.JobDefinitionName)
		}

		jobID := jb.GetID()
		var sourceID source.ID
		if err := sourceID.FromString(jobID.String()); err != nil {
			return in, fmt.Errorf("failed to calculate source id: %w", err)
		}

		in = input.NewInput(input.Artifact, sourceID)
	default:
		return in, fmt.Errorf("unknown input type: %s", def.GetType())
	}

	return in, nil
}

func (f *ExecutionFactory) calculateSourceID(vars ...string) (source.ID, error) {
	var id source.ID

	hash := sha1.New()
	for _, v := range vars {
		hash.Write([]byte(v))
	}

	if err := id.FromString(fmt.Sprintf("%x", hash.Sum(nil))); err != nil {
		return id, err
	}

	return id, nil
}

func (f *ExecutionFactory) calculateJobID(vars ...string) (job.ID, error) {
	var id job.ID

	hash := sha1.New()
	for _, v := range vars {
		hash.Write([]byte(v))
	}

	if err := id.FromString(fmt.Sprintf("%x", hash.Sum(nil))); err != nil {
		return id, err
	}

	return id, nil
}
