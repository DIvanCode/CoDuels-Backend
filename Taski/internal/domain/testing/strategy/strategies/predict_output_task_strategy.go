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

type PredictOutputTaskTestingStrategy struct {
	strategy.Details
}

func NewPredictOutputTaskTestingStrategy(
	t task.Task,
	taskSource sources.Source,
	solution string,
) (TestingStrategy, error) {
	ts := TestingStrategy{}

	typedTask, ok := t.(*tasks.PredictOutputTask)
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

	suspectOutput := inputs.NewInlineInput(suspectSolutionSource.GetName())
	correctOutput := inputs.NewFilestorageBucketInput(taskSource.GetName(), typedTask.Test.Output)
	checkJobName := strategy.FormatJobName(strategy.CheckJobFormat)
	checkJob, err := strategy.NewCheckJob(checkJobName,
		job.StatusOK,
		checkerDef.Lang, checker,
		suspectOutput, correctOutput)
	if err != nil {
		return ts, fmt.Errorf("failed to run checker: %w", err)
	}

	checkStage := execution.Stage{
		Name: strategy.FormatStageName(strategy.CheckStageFormat),
		Deps: []execution.StageName{prepareStage.Name},
		Jobs: []jobs.Job{checkJob},
	}

	stages := []execution.Stage{prepareStage, checkStage}

	ts.ITestingStrategy = &PredictOutputTaskTestingStrategy{
		Details: strategy.Details{
			TaskType: task.PredictOutput,
			Stages:   stages,
			Sources:  srcs,

			JobSuccess: make(map[job.Name]bool),
		},
	}

	return ts, nil
}
