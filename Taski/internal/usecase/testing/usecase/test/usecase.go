package test

import (
	"context"
	"fmt"
	"log/slog"
	"strconv"
	"taski/internal/domain/task"
	"taski/internal/domain/task/tasks"
	"taski/internal/domain/testing"
	"taski/internal/domain/testing/sources"
	"taski/internal/domain/testing/steps"

	"github.com/DIvanCode/filestorage/pkg/bucket"
)

type (
	Command struct {
		TaskID     task.ID
		SolutionID testing.SolutionID
		Solution   string
		Lang       task.Language
	}

	UseCase struct {
		log                  *slog.Logger
		taskStorage          taskStorage
		unitOfWork           unitOfWork
		solutionStorage      solutionStorage
		executeClient        executeClient
		downloadTaskEndpoint string
	}

	taskStorage interface {
		Get(task.ID) (task task.Task, unlock func(), err error)
		GetTaskBucket(id task.ID) (bucketID bucket.ID, err error)
	}

	unitOfWork interface {
		Do(context.Context, func(ctx context.Context) error) error
	}

	solutionStorage interface {
		Create(context.Context, testing.Solution) error
	}

	executeClient interface {
		Execute(context.Context, []testing.Step) (testing.ExecutionID, error)
	}
)

func NewUseCase(
	log *slog.Logger,
	storage taskStorage,
	unitOfWork unitOfWork,
	solutionStorage solutionStorage,
	executeClient executeClient,
	downloadTaskEndpoint string,
) *UseCase {
	return &UseCase{
		log:                  log,
		taskStorage:          storage,
		unitOfWork:           unitOfWork,
		solutionStorage:      solutionStorage,
		executeClient:        executeClient,
		downloadTaskEndpoint: downloadTaskEndpoint,
	}
}

func (uc *UseCase) Test(ctx context.Context, command Command) error {
	return uc.unitOfWork.Do(ctx, func(ctx context.Context) error {
		t, unlock, err := uc.taskStorage.Get(command.TaskID)
		if err != nil {
			uc.log.Error("failed to get task from storage", slog.Any("err", err))
			return fmt.Errorf("failed to get task from storage")
		}
		defer unlock()

		testingSteps, testsStatus, err := uc.CreateTestingSteps(t, command.Solution, command.Lang)
		if err != nil {
			uc.log.Error("failed to create testing steps", slog.Any("err", err))
			return fmt.Errorf("failed to create testing steps")
		}

		executionID, err := uc.executeClient.Execute(ctx, testingSteps)
		if err != nil {
			uc.log.Error("failed to execute testing steps", slog.Any("err", err))
			return fmt.Errorf("failed to execute testing steps")
		}

		sol := testing.Solution{
			ID:          command.SolutionID,
			ExecutionID: executionID,
			TaskID:      command.TaskID,
			Solution:    command.Solution,
			Lang:        command.Lang,
			Tests:       len(testsStatus),
			Status:      testsStatus,
		}
		if err := uc.solutionStorage.Create(ctx, sol); err != nil {
			uc.log.Error("failed to save solution to storage", slog.String("solution_id", string(sol.ID)), slog.Any("err", err))
			return fmt.Errorf("failed to save solution to storage: %w", err)
		}

		return nil
	})
}

func (uc *UseCase) CreateTestingSteps(t task.Task, solution string, lang task.Language) ([]testing.Step, map[int]string, error) {
	taskBucket, err := uc.taskStorage.GetTaskBucket(t.GetID())
	if err != nil {
		return nil, nil, fmt.Errorf("failed to get task bucket: %w", err)
	}

	testingSteps := make([]testing.Step, 0)
	testsStatus := make(map[int]string)
	switch t.GetType() {
	case task.WriteCode:
		t := t.(*tasks.WriteCodeTask)

		testsSteps := make(map[int][]testing.Step, len(t.Tests))

		inputs := make(map[int]testing.Source, len(t.Tests))
		for _, test := range t.Tests {
			inputs[test.ID] = sources.NewFilestorageBucketSource(
				taskBucket,
				uc.downloadTaskEndpoint,
				test.Input)
			testsStatus[test.ID] = "?"
			testsSteps[test.ID] = make([]testing.Step, 0)
		}

		suspect := sources.NewInlineSource(solution)
		suspectOutputs := make(map[int]testing.Source, len(t.Tests))
		switch lang {
		case task.LanguageCpp:
			compileStep := steps.NewCompileCppStep("compile_suspect", suspect)
			testingSteps = append(testingSteps, compileStep)
			compiledCode := sources.NewOtherStepSource(compileStep.Name)

			for _, test := range t.Tests {
				runStep := steps.NewRunCppStep(
					"run_suspect_"+strconv.Itoa(test.ID),
					compiledCode,
					inputs[test.ID],
					t.TimeLimit,
					t.MemoryLimit)
				testsSteps[test.ID] = append(testsSteps[test.ID], runStep)
				suspectOutputs[test.ID] = sources.NewOtherStepSource(runStep.Name)
			}
		case task.LanguagePython:
			for _, test := range t.Tests {
				runStep := steps.NewRunPyStep(
					"run_suspect_"+strconv.Itoa(test.ID),
					suspect,
					inputs[test.ID],
					t.TimeLimit,
					t.MemoryLimit)
				testsSteps[test.ID] = append(testsSteps[test.ID], runStep)
				suspectOutputs[test.ID] = sources.NewOtherStepSource(runStep.Name)
			}
		case task.LanguageGo:
			for _, test := range t.Tests {
				runStep := steps.NewRunGoStep(
					"run_suspect_"+strconv.Itoa(test.ID),
					suspect,
					inputs[test.ID],
					t.TimeLimit,
					t.MemoryLimit)
				testsSteps[test.ID] = append(testsSteps[test.ID], runStep)
				suspectOutputs[test.ID] = sources.NewOtherStepSource(runStep.Name)
			}
		}

		correct := sources.NewFilestorageBucketSource(taskBucket, uc.downloadTaskEndpoint, t.Solution.Path)
		correctOutputs := make(map[int]testing.Source, len(t.Tests))
		switch t.Solution.Lang {
		case task.LanguageCpp:
			compileStep := steps.NewCompileCppStep("compile_correct", correct)
			testingSteps = append(testingSteps, compileStep)
			compiledCode := sources.NewOtherStepSource(compileStep.Name)

			for _, test := range t.Tests {
				runStep := steps.NewRunCppStep(
					"run_correct_"+strconv.Itoa(test.ID),
					compiledCode,
					inputs[test.ID],
					t.TimeLimit,
					t.MemoryLimit)
				testsSteps[test.ID] = append(testsSteps[test.ID], runStep)
				correctOutputs[test.ID] = sources.NewOtherStepSource(runStep.Name)
			}
		case task.LanguagePython:
			for _, test := range t.Tests {
				runStep := steps.NewRunPyStep(
					"run_correct_"+strconv.Itoa(test.ID),
					correct,
					inputs[test.ID],
					t.TimeLimit,
					t.MemoryLimit)
				testsSteps[test.ID] = append(testsSteps[test.ID], runStep)
				correctOutputs[test.ID] = sources.NewOtherStepSource(runStep.Name)
			}
		case task.LanguageGo:
			for _, test := range t.Tests {
				runStep := steps.NewRunGoStep(
					"run_correct_"+strconv.Itoa(test.ID),
					correct,
					inputs[test.ID],
					t.TimeLimit,
					t.MemoryLimit)
				testsSteps[test.ID] = append(testsSteps[test.ID], runStep)
				correctOutputs[test.ID] = sources.NewOtherStepSource(runStep.Name)
			}
		}

		checker := sources.NewFilestorageBucketSource(taskBucket, uc.downloadTaskEndpoint, t.Checker.Path)
		switch t.Checker.Lang {
		case task.LanguageCpp:
			compileStep := steps.NewCompileCppStep("compile_checker", checker)
			testingSteps = append(testingSteps, compileStep)
			compiledChecker := sources.NewOtherStepSource(compileStep.Name)

			for _, test := range t.Tests {
				checkStep := steps.NewCheckCppStep(
					"check_"+strconv.Itoa(test.ID),
					compiledChecker,
					correctOutputs[test.ID],
					suspectOutputs[test.ID])
				testsSteps[test.ID] = append(testsSteps[test.ID], checkStep)
			}
		}

		for _, test := range t.Tests {
			for _, step := range testsSteps[test.ID] {
				testingSteps = append(testingSteps, step)
			}
		}
	default:
		return nil, nil, fmt.Errorf("cannot create testing steps for task of type %s", t.GetType())
	}

	return testingSteps, testsStatus, nil
}
