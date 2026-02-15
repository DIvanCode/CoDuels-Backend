package strategies

import (
	"fmt"
	"regexp"
	"strconv"
	"strings"
	"taski/internal/domain/task"
	"taski/internal/domain/task/tasks"
	"taski/internal/domain/testing/execution"
	"taski/internal/domain/testing/input/inputs"
	"taski/internal/domain/testing/job"
	"taski/internal/domain/testing/job/jobs"
	"taski/internal/domain/testing/source/sources"
	"taski/internal/domain/testing/strategy"
)

type WriteCodeTaskTestingStrategy struct {
	strategy.Details
	TestsCount int
	TestStatus map[int]job.Status
}

const (
	testsStageFormat string = "tests %d-%d"

	runOnTestJobFormat   string = "run %s on test %d"
	checkOnTestJobFormat string = "check suspect on test %d"
)

var testRegex = regexp.MustCompile(`\s*test\s*(\d+)$`)
var runJobRegex = regexp.MustCompile(`^run\s*`)
var checkJobRegex = regexp.MustCompile(`^check\s*`)

const (
	wrongAnswerVerdictFormat  string = "Wrong Answer on test %d"
	runtimeErrorVerdictFormat string = "Runtime Error on test %d"
	timeLimitVerdictFormat    string = "Time Limit on test %d"
	memoryLimitVerdictFormat  string = "Memory Limit on test %d"

	testingOnTestStatusFormat string = "Testing on test %d"
)

func NewWriteCodeTaskTestingStrategy(
	t task.Task,
	taskSource sources.Source,
	solution string,
	lang task.Language,
) (TestingStrategy, error) {
	ts := TestingStrategy{}

	typedTask, ok := t.(*tasks.WriteCodeTask)
	if !ok {
		return ts, fmt.Errorf("unsupported task type %s", t.GetType())
	}

	suspectCodeSource := sources.NewInlineSource(strategy.SuspectSolutionSource, solution)

	srcs := sources.Sources{taskSource, suspectCodeSource}
	stages := make([]execution.Stage, 0)

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

	suspectCode := inputs.NewInlineInput(suspectCodeSource.GetName())
	prepareSuspectCodeJobName := strategy.FormatJobName(strategy.PrepareJobFormat, strategy.SuspectCode)
	prepareSuspectCodeJob, err := strategy.NewPrepareJob(prepareSuspectCodeJobName, suspectCode, lang)
	if err != nil {
		return ts, fmt.Errorf("failed to prepare suspect code: %w", err)
	}
	if prepareSuspectCodeJob != nil {
		prepareStage.Jobs = append(prepareStage.Jobs, *prepareSuspectCodeJob)
		suspectCode = inputs.NewArtifactInput(prepareSuspectCodeJob.GetName())
	}

	stages = append(stages, prepareStage)

	tests := make(map[int]task.Test)
	for _, test := range typedTask.Tests {
		tests[test.ID] = test
	}

	testsInBatch := 5
	testBatches := (len(typedTask.Tests) + testsInBatch - 1) / testsInBatch
	for batch := range testBatches {
		from := batch*testsInBatch + 1
		to := min(len(typedTask.Tests), (batch+1)*testsInBatch)
		deps := make([]execution.StageName, 0, len(stages))
		for _, dep := range stages {
			deps = append(deps, dep.Name)
		}
		batchStage := execution.Stage{
			Name: strategy.FormatStageName(testsStageFormat, from, to),
			Deps: deps,
			Jobs: []jobs.Job{},
		}

		for id := from; id <= to; id++ {
			test, ok := tests[id]
			if !ok {
				return ts, fmt.Errorf("failed to find test %d (test ids must be permutation)", id)
			}

			testInput := inputs.NewFilestorageBucketInput(taskSource.GetName(), test.Input)
			runSuspectJobName := strategy.FormatJobName(runOnTestJobFormat, strategy.SuspectCode, test.ID)
			runSuspectJob, err := strategy.NewRunJob(runSuspectJobName,
				lang, suspectCode, testInput,
				typedTask.TimeLimit, typedTask.MemoryLimit, false)
			if err != nil {
				return ts, fmt.Errorf("failed to run suspect job: %w", err)
			}
			batchStage.Jobs = append(batchStage.Jobs, runSuspectJob)
			suspectOutput := inputs.NewArtifactInput(runSuspectJob.GetName())

			correctOutput := inputs.NewFilestorageBucketInput(taskSource.GetName(), test.Output)
			checkJobName := strategy.FormatJobName(checkOnTestJobFormat, test.ID)
			checkJob, err := strategy.NewCheckJob(checkJobName,
				job.StatusOK,
				checkerDef.Lang, checker,
				suspectOutput, correctOutput)
			if err != nil {
				return ts, fmt.Errorf("failed to run checker: %w", err)
			}
			batchStage.Jobs = append(batchStage.Jobs, checkJob)
		}

		stages = append(stages, batchStage)
	}

	ts.ITestingStrategy = &WriteCodeTaskTestingStrategy{
		Details: strategy.Details{
			TaskType: task.WriteCode,
			Stages:   stages,
			Sources:  srcs,
		},
		TestsCount: len(typedTask.Tests),
		TestStatus: make(map[int]job.Status),
	}

	return ts, nil
}

func (ts *WriteCodeTaskTestingStrategy) UpdateJobStatus(name job.Name, status job.Status) {
	if ts.Verdict != nil {
		return
	}

	jb, ok := ts.FindJob(name)
	if !ok {
		return
	}

	isSuccess := status == jb.GetSuccessStatus()
	isSuspectJob := strategy.IsSuspectJob(name)

	testID, isTest := ts.parseTestID(name)
	if isSuspectJob && isTest {
		if ts.isRunJob(name) && !isSuccess {
			ts.TestStatus[testID] = status
		}
		if ts.isCheckJob(name) {
			ts.TestStatus[testID] = status
		}
	}

	if !isSuccess {
		if !isSuspectJob {
			verdict := strategy.TestingFailedVerdict
			ts.Verdict = &verdict
			return
		}

		if !isTest {
			verdict := ts.verdictForStatus(status, 0)
			ts.Verdict = &verdict
			return
		}

		if ts.previousTestsChecked(testID) {
			failedTestID, failedTestStatus := ts.findFirstFailedTest()
			verdict := ts.verdictForStatus(failedTestStatus, failedTestID)
			ts.Verdict = &verdict
			return
		}
	}

	if !ts.allTestsChecked() {
		return
	}

	if ts.allTestsPassed() {
		verdict := strategy.AcceptedVerdict
		ts.Verdict = &verdict
		return
	}

	failedTestID, failedTestStatus := ts.findFirstFailedTest()
	verdict := ts.verdictForStatus(failedTestStatus, failedTestID)
	ts.Verdict = &verdict
	return
}

func (ts *WriteCodeTaskTestingStrategy) GetTestingStatus() string {
	checkedTests := ts.findMostPassedPrefix()
	if checkedTests < ts.TestsCount {
		return fmt.Sprintf(testingOnTestStatusFormat, checkedTests+1)
	}
	return ts.Details.GetTestingStatus()
}

func (ts *WriteCodeTaskTestingStrategy) parseTestID(name job.Name) (int, bool) {
	matches := testRegex.FindStringSubmatch(strings.ToLower(string(name)))
	if len(matches) != 2 {
		return 0, false
	}
	id, err := strconv.Atoi(matches[1])
	if err != nil {
		return 0, false
	}
	return id, true
}

func (ts *WriteCodeTaskTestingStrategy) isRunJob(name job.Name) bool {
	return runJobRegex.MatchString(strings.ToLower(string(name)))
}

func (ts *WriteCodeTaskTestingStrategy) isCheckJob(name job.Name) bool {
	return checkJobRegex.MatchString(strings.ToLower(string(name)))
}

func (ts *WriteCodeTaskTestingStrategy) previousTestsChecked(testID int) bool {
	if testID <= 1 {
		return true
	}

	for previousTestID := 1; previousTestID < testID; previousTestID++ {
		_, ok := ts.TestStatus[previousTestID]
		if !ok {
			return false
		}
	}

	return true
}

func (ts *WriteCodeTaskTestingStrategy) allTestsChecked() bool {
	for testID := 1; testID <= ts.TestsCount; testID++ {
		_, ok := ts.TestStatus[testID]
		if !ok {
			return false
		}
	}
	return true
}

func (ts *WriteCodeTaskTestingStrategy) allTestsPassed() bool {
	for testID := 1; testID <= ts.TestsCount; testID++ {
		status, ok := ts.TestStatus[testID]
		if !ok || status != job.StatusOK {
			return false
		}
	}
	return true
}

func (ts *WriteCodeTaskTestingStrategy) findFirstFailedTest() (int, job.Status) {
	for testID := 1; testID <= ts.TestsCount; testID++ {
		status := ts.TestStatus[testID]
		if status != job.StatusOK {
			return testID, status
		}
	}
	return 0, job.StatusOK
}

func (ts *WriteCodeTaskTestingStrategy) findMostPassedPrefix() int {
	for testID := 1; testID <= ts.TestsCount; testID++ {
		status, ok := ts.TestStatus[testID]
		if !ok || status != job.StatusOK {
			return testID - 1
		}
	}
	return ts.TestsCount
}

func (ts *WriteCodeTaskTestingStrategy) verdictForStatus(status job.Status, testID int) string {
	switch status {
	case job.StatusTL:
		return fmt.Sprintf(timeLimitVerdictFormat, testID)
	case job.StatusML:
		return fmt.Sprintf(memoryLimitVerdictFormat, testID)
	case job.StatusRE:
		return fmt.Sprintf(runtimeErrorVerdictFormat, testID)
	case job.StatusWA:
		return fmt.Sprintf(wrongAnswerVerdictFormat, testID)
	default:
		return ts.Details.VerdictForStatus(status)
	}
}
