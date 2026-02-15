package strategies

import (
	"fmt"
	"taski/internal/domain/task"
	"taski/internal/domain/task/tasks"
	"taski/internal/domain/testing/execution"
	"taski/internal/domain/testing/input/inputs"
	"taski/internal/domain/testing/job"
	"taski/internal/domain/testing/job/jobs"
	"taski/internal/domain/testing/source/sources"
	"taski/internal/domain/testing/strategy"
)

type FindTestTaskTestingStrategy struct {
	strategy.Details
}

func NewFindTestTaskTestingStrategy(
	t task.Task,
	taskSource sources.Source,
	solution string,
) (TestingStrategy, error) {
	ts := TestingStrategy{}

	typedTask, ok := t.(*tasks.FindTestTask)
	if !ok {
		return ts, fmt.Errorf("unsupported task type %s", t.GetType())
	}

	suspectSolutionSource := sources.NewInlineSource(strategy.SuspectSolutionSource, solution)

	srcs := sources.Sources{taskSource, suspectSolutionSource}

	prepareStage := execution.Stage{
		Name: strategy.FormatStageName(strategy.PrepareStageFormat),
		Deps: []execution.StageName{},
		Jobs: []jobs.Job{},
	}

	checkerDef := typedTask.Checker
	checker := inputs.NewFilestorageBucketInput(taskSource.GetName(), checkerDef.Path)
	prepareCheckerJobName := strategy.FormatJobName(strategy.PrepareJobFormat, strategy.CheckerCode)
	prepareCheckerJob, err := strategy.NewPrepareJob(prepareCheckerJobName, checker, checkerDef.Lang)
	if err != nil {
		return ts, fmt.Errorf("failed to prepare checker: %w", err)
	}
	if prepareCheckerJob != nil {
		prepareStage.Jobs = append(prepareStage.Jobs, *prepareCheckerJob)
		checker = inputs.NewArtifactInput(prepareCheckerJob.GetName())
	}

	sourceCodeDef := typedTask.Code
	sourceCode := inputs.NewFilestorageBucketInput(taskSource.GetName(), sourceCodeDef.Path)
	prepareSourceCodeJobName := strategy.FormatJobName(strategy.PrepareJobFormat, strategy.SourceCode)
	prepareSourceCodeJob, err := strategy.NewPrepareJob(prepareSourceCodeJobName, sourceCode, sourceCodeDef.Lang)
	if err != nil {
		return ts, fmt.Errorf("failed to prepare source code: %w", err)
	}
	if prepareSourceCodeJob != nil {
		prepareStage.Jobs = append(prepareStage.Jobs, *prepareSourceCodeJob)
		sourceCode = inputs.NewArtifactInput(prepareSourceCodeJob.GetName())
	}

	solutionCodeDef := typedTask.Solution
	solutionCode := inputs.NewFilestorageBucketInput(taskSource.GetName(), solutionCodeDef.Path)
	prepareSolutionCodeJobName := strategy.FormatJobName(strategy.PrepareJobFormat, strategy.SolutionCode)
	prepareSolutionCodeJob, err := strategy.NewPrepareJob(prepareSolutionCodeJobName, solutionCode, solutionCodeDef.Lang)
	if err != nil {
		return ts, fmt.Errorf("failed to prepare solution code: %w", err)
	}
	if prepareSolutionCodeJob != nil {
		prepareStage.Jobs = append(prepareStage.Jobs, *prepareSolutionCodeJob)
		solutionCode = inputs.NewArtifactInput(prepareSolutionCodeJob.GetName())
	}

	input := inputs.NewInlineInput(suspectSolutionSource.GetName())

	runSourceCodeJobName := strategy.FormatJobName(strategy.RunJobFormat, strategy.SourceCode)
	runSourceCodeJob, err := strategy.NewRunJob(runSourceCodeJobName,
		sourceCodeDef.Lang, sourceCode, input,
		typedTask.TimeLimit, typedTask.MemoryLimit, false)
	if err != nil {
		return ts, fmt.Errorf("failed to run source code job: %w", err)
	}
	sourceCodeOutput := inputs.NewArtifactInput(runSourceCodeJob.GetName())

	runSolutionCodeJobName := strategy.FormatJobName(strategy.RunJobFormat, strategy.SolutionCode)
	runSolutionCodeJob, err := strategy.NewRunJob(runSolutionCodeJobName,
		solutionCodeDef.Lang, solutionCode, input,
		typedTask.TimeLimit, typedTask.MemoryLimit, false)
	if err != nil {
		return ts, fmt.Errorf("failed to run solution code job: %w", err)
	}
	solutionCodeOutput := inputs.NewArtifactInput(runSolutionCodeJob.GetName())

	checkJobName := strategy.FormatJobName(strategy.CheckJobFormat)
	checkJob, err := strategy.NewCheckJob(checkJobName,
		job.StatusWA,
		checkerDef.Lang, checker,
		sourceCodeOutput, solutionCodeOutput)
	if err != nil {
		return ts, fmt.Errorf("failed to run checker: %w", err)
	}

	checkStage := execution.Stage{
		Name: strategy.FormatStageName(strategy.CheckStageFormat),
		Deps: []execution.StageName{prepareStage.Name},
		Jobs: []jobs.Job{runSourceCodeJob, runSolutionCodeJob, checkJob},
	}

	stages := []execution.Stage{prepareStage, checkStage}

	ts.ITestingStrategy = &FindTestTaskTestingStrategy{
		Details: strategy.Details{
			TaskType: task.FindTest,
			Stages:   stages,
			Sources:  srcs,

			JobSuccess: make(map[job.Name]bool),
		},
	}

	return ts, nil
}
