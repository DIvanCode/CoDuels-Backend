package strategy

import (
	"fmt"
	"regexp"
	"strings"
	"taski/internal/domain/task"
	"taski/internal/domain/testing/execution"
	"taski/internal/domain/testing/input/inputs"
	"taski/internal/domain/testing/job"
	"taski/internal/domain/testing/job/jobs"
	"taski/internal/domain/testing/source"
	"taski/internal/domain/testing/source/sources"
)

type (
	ITestingStrategy interface {
		GetTaskType() task.Type
		GetStages() execution.Stages
		GetSources() sources.Sources
		GetVerdict() string
		GetMessage() *string
		UpdateJobStatus(name job.Name, status job.Status, msg *string)
		GetTestingStatus() string
	}

	Details struct {
		TaskType task.Type        `json:"task_type"`
		Stages   execution.Stages `json:"stages"`
		Sources  sources.Sources  `json:"sources"`
		Verdict  *string          `json:"verdict"`
		Message  *string          `json:"message"`

		JobSuccess map[job.Name]bool
	}
)

const (
	TaskSource            source.Name = "task"
	SuspectSolutionSource source.Name = "suspect solution"

	CheckerCode  string = "checker code"
	SuspectCode  string = "suspect code"
	SourceCode   string = "source code"
	SolutionCode string = "solution code"

	PrepareStageFormat = "prepare"
	CheckStageFormat   = "check"

	PrepareJobFormat string = "prepare %s"
	RunJobFormat     string = "run %s"
	CheckJobFormat   string = "[suspect] check"

	TestingFailedVerdict    string = "Testing Failed"
	CompilationErrorVerdict string = "Compilation Error"
	WrongAnswerVerdict      string = "Wrong Answer"
	AcceptedVerdict         string = "Accepted"

	TestingInProgressStatus string = "Testing in progress"
)

var (
	suspectRegex = regexp.MustCompile(`\s*suspect\s*`)
)

func (ts *Details) GetTaskType() task.Type {
	return ts.TaskType
}

func (ts *Details) GetStages() execution.Stages {
	return ts.Stages
}

func (ts *Details) GetSources() sources.Sources {
	return ts.Sources
}

func (ts *Details) GetTestingStatus() string {
	return TestingInProgressStatus
}

func (ts *Details) GetVerdict() string {
	if ts.Verdict != nil {
		return *ts.Verdict
	}
	return TestingFailedVerdict
}

func (ts *Details) GetMessage() *string {
	return ts.Message
}

func (ts *Details) FindJob(name job.Name) (jobs.Job, bool) {
	for _, stage := range ts.Stages {
		for _, jb := range stage.Jobs {
			if jb.GetName() == name {
				return jb, true
			}
		}
	}
	return jobs.Job{}, false
}

func (ts *Details) VerdictForStatus(status job.Status) string {
	switch status {
	case job.StatusCE:
		return CompilationErrorVerdict
	default:
		return WrongAnswerVerdict
	}
}

func (ts *Details) UpdateJobStatus(name job.Name, status job.Status, msg *string) {
	if ts.Verdict != nil {
		return
	}

	if msg != nil {
		ts.Message = msg
	}

	jb, ok := ts.FindJob(name)
	if !ok {
		return
	}

	isSuccess := status == jb.GetSuccessStatus()
	ts.JobSuccess[name] = isSuccess

	if !isSuccess {
		if !IsSuspectJob(name) {
			verdict := TestingFailedVerdict
			ts.Verdict = &verdict
			return
		}

		verdict := ts.VerdictForStatus(status)
		ts.Verdict = &verdict
		return
	}

	if ts.AllJobsDone() {
		verdict := AcceptedVerdict
		ts.Verdict = &verdict
		return
	}
}

func (ts *Details) AllJobsDone() bool {
	for _, stage := range ts.Stages {
		for _, jb := range stage.Jobs {
			if _, ok := ts.JobSuccess[jb.GetName()]; !ok {
				return false
			}
		}
	}
	return true
}

func FormatStageName(format string, args ...any) execution.StageName {
	return execution.StageName(fmt.Sprintf(format, args...))
}

func FormatJobName(format string, args ...any) job.Name {
	return job.Name(fmt.Sprintf(format, args...))
}

func IsSuspectJob(name job.Name) bool {
	return suspectRegex.MatchString(strings.ToLower(string(name)))
}

func NewPrepareJob(name job.Name, code inputs.Input, lang task.Language) (*jobs.Job, error) {
	switch lang {
	case task.LanguageCpp:
		jb := jobs.NewCompileCppJob(name, code)
		return &jb, nil
	case task.LanguageGo:
		jb := jobs.NewCompileGoJob(name, code)
		return &jb, nil
	case task.LanguagePython:
		return nil, nil
	default:
		return nil, fmt.Errorf("unsupported language: %s", lang)
	}
}

func NewRunJob(name job.Name,
	lang task.Language, code inputs.Input, input inputs.Input,
	timeLimit int, memoryLimit int, showOutput bool,
) (jobs.Job, error) {
	switch lang {
	case task.LanguageCpp:
		return jobs.NewRunCppJob(name, code, input, timeLimit, memoryLimit, showOutput), nil
	case task.LanguageGo:
		return jobs.NewRunGoJob(name, code, input, timeLimit, memoryLimit, showOutput), nil
	case task.LanguagePython:
		return jobs.NewRunPyJob(name, code, input, timeLimit, memoryLimit, showOutput), nil
	default:
		return jobs.Job{}, fmt.Errorf("unsupported language: %s", lang)
	}
}

func NewCheckJob(name job.Name,
	successStatus job.Status,
	lang task.Language, checker inputs.Input,
	correctOutput inputs.Input, suspectOutput inputs.Input,
) (jobs.Job, error) {
	switch lang {
	case task.LanguageCpp:
		return jobs.NewCheckCppJob(name, successStatus, checker, correctOutput, suspectOutput), nil
	default:
		return jobs.Job{}, fmt.Errorf("unsupported language: %s", lang)
	}
}
