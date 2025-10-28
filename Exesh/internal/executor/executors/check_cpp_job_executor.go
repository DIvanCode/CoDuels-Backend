package executors

import (
	"bytes"
	"context"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/jobs"
	"exesh/internal/domain/execution/results"
	"exesh/internal/runtime"
	"fmt"
	"io"
	"log/slog"
	"time"
)

type CheckCppJobExecutor struct {
	log            *slog.Logger
	inputProvider  inputProvider
	outputProvider outputProvider
	runtime        runtime.Runtime
}

func NewCheckCppJobExecutor(log *slog.Logger, inputProvider inputProvider, outputProvider outputProvider, rt runtime.Runtime) *CheckCppJobExecutor {
	return &CheckCppJobExecutor{
		log:            log,
		inputProvider:  inputProvider,
		outputProvider: outputProvider,
		runtime:        rt,
	}
}

func (e *CheckCppJobExecutor) SupportsType(jobType execution.JobType) bool {
	return jobType == execution.CheckCppJobType
}

func (e *CheckCppJobExecutor) Execute(ctx context.Context, job execution.Job) execution.Result {
	errorResult := func(err error) execution.Result {
		return results.CheckResult{
			ResultDetails: execution.ResultDetails{
				ID:     job.GetID(),
				Type:   execution.CheckResult,
				DoneAt: time.Now(),
				Error:  err.Error(),
			},
		}
	}

	okResult := func() execution.Result {
		return results.CheckResult{
			ResultDetails: execution.ResultDetails{
				ID:     job.GetID(),
				Type:   execution.CheckResult,
				DoneAt: time.Now(),
			},
			Status: results.CheckStatusOK,
		}
	}

	wrongAnswerResult := func() execution.Result {
		return results.CheckResult{
			ResultDetails: execution.ResultDetails{
				ID:     job.GetID(),
				Type:   execution.CheckResult,
				DoneAt: time.Now(),
			},
			Status: results.CheckStatusWA,
		}
	}

	if job.GetType() != execution.CheckCppJobType {
		return errorResult(fmt.Errorf("unsupported job type %s for %s executor", job.GetType(), execution.CheckCppJobType))
	}
	checkCppJob := job.(*jobs.CheckCppJob)

	_, unlock, err := e.inputProvider.Locate(ctx, checkCppJob.CorrectOutput)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate correct_output input: %w", err))
	}
	unlock()
	_, unlock, err = e.inputProvider.Locate(ctx, checkCppJob.SuspectOutput)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate suspect_output input: %w", err))
	}
	unlock()
	_, unlock, err = e.inputProvider.Locate(ctx, checkCppJob.CompiledChecker)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate compiled_checker input: %w", err))
	}
	unlock()

	var compiledCheckerLocation string
	compiledCheckerLocation, unlock, err = e.inputProvider.Locate(ctx, checkCppJob.CompiledChecker)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate compiled_checker input: %w", err))
	}
	defer unlock()

	var correctOutputLocation string
	correctOutputLocation, unlock, err = e.inputProvider.Locate(ctx, checkCppJob.CorrectOutput)
	if err != nil {
		return errorResult(fmt.Errorf("failed to read correct_output input: %w", err))
	}
	defer unlock()

	var suspectOutputLocation string
	suspectOutputLocation, unlock, err = e.inputProvider.Locate(ctx, checkCppJob.SuspectOutput)
	if err != nil {
		return errorResult(fmt.Errorf("failed to read suspect_output input: %w", err))
	}
	defer unlock()

	var checkVerdict io.Writer
	checkVerdict, commit, abort, err := e.outputProvider.Create(ctx, checkCppJob.CheckVerdict)
	if err != nil {
		return errorResult(fmt.Errorf("failed to create check_verdict output: %w", err))
	}

	stderr := bytes.NewBuffer(nil)
	err = e.runtime.Execute(ctx, []string{"/a.out", "/correct.txt", "/suspect.txt"}, runtime.ExecuteParams{
		InFiles: []runtime.File{
			{InsideLocation: "/correct.txt", OutsideLocation: correctOutputLocation},
			{InsideLocation: "/suspect.txt", OutsideLocation: suspectOutputLocation},
			{InsideLocation: "/a.out", OutsideLocation: compiledCheckerLocation},
		},
		Stdout: checkVerdict,
		Stderr: stderr,
	})
	if err != nil {
		e.log.Error("execute checker in runtime error", slog.Any("err", err))
		return errorResult(err)
	}

	e.log.Info("command ok")

	if err = commit(); err != nil {
		_ = abort()
		return errorResult(fmt.Errorf("failed to commit check_verdict output creation: %w", err))
	}

	checkVerdictReader, unlock, err := e.outputProvider.Read(ctx, checkCppJob.CheckVerdict)
	if err != nil {
		return errorResult(fmt.Errorf("failed to open check_verdict output: %w", err))
	}

	defer unlock()

	checkVerdictOutput, err := io.ReadAll(checkVerdictReader)
	if err != nil {
		return errorResult(fmt.Errorf("failed to read check_verdict output: %w", err))
	}

	if string(checkVerdictOutput) == string(results.CheckStatusOK) {
		return okResult()
	}
	if string(checkVerdictOutput) == string(results.CheckStatusWA) {
		return wrongAnswerResult()
	}
	return errorResult(fmt.Errorf("failed to parse check_verdict output: %s", string(checkVerdictOutput)))
}
