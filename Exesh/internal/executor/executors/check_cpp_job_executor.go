package executors

import (
	"bytes"
	"context"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/jobs"
	"exesh/internal/domain/execution/results"
	"fmt"
	"log/slog"
	"os/exec"
	"time"
)

type CheckCppJobExecutor struct {
	log            *slog.Logger
	inputProvider  inputProvider
	outputProvider outputProvider
}

func NewCheckCppJobExecutor(log *slog.Logger, inputProvider inputProvider, outputProvider outputProvider) *CheckCppJobExecutor {
	return &CheckCppJobExecutor{
		log:            log,
		inputProvider:  inputProvider,
		outputProvider: outputProvider,
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

	compiledChecker, unlock, err := e.inputProvider.Locate(ctx, checkCppJob.CompiledChecker)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate compiled_checker input: %w", err))
	}
	defer unlock()

	correctOutput, unlock, err := e.inputProvider.Locate(ctx, checkCppJob.CorrectOutput)
	if err != nil {
		return errorResult(fmt.Errorf("failed to read correct_output input: %w", err))
	}
	defer unlock()

	suspectOutput, unlock, err := e.inputProvider.Locate(ctx, checkCppJob.SuspectOutput)
	if err != nil {
		return errorResult(fmt.Errorf("failed to read suspect_output input: %w", err))
	}
	defer unlock()

	cmd := exec.CommandContext(ctx, "./"+compiledChecker, correctOutput, suspectOutput)

	checkVerdict := bytes.Buffer{}
	cmd.Stdout = &checkVerdict

	e.log.Info("do command", slog.Any("cmd", cmd))
	if err = cmd.Run(); err != nil {
		e.log.Info("command error", slog.Any("err", err))
		return errorResult(err)
	}

	e.log.Info("command ok")

	checkVerdictOutput := string(checkVerdict.Bytes())

	if checkVerdictOutput == string(results.CheckStatusOK) {
		return okResult()
	}
	if checkVerdictOutput == string(results.CheckStatusWA) {
		return wrongAnswerResult()
	}
	return errorResult(fmt.Errorf("failed to parse check_verdict output: %s", string(checkVerdictOutput)))
}
