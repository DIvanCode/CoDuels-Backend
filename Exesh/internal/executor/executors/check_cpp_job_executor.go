package executors

import (
	"bytes"
	"context"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/runtime"
	"fmt"
	"io"
	"log/slog"
)

type CheckCppJobExecutor struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	runtime        runtime.Runtime
}

func NewCheckCppJobExecutor(
	log *slog.Logger,
	sourceProvider sourceProvider,
	outputProvider outputProvider,
	runtime runtime.Runtime,
) *CheckCppJobExecutor {
	return &CheckCppJobExecutor{
		log:            log,
		sourceProvider: sourceProvider,
		outputProvider: outputProvider,
		runtime:        runtime,
	}
}

func (e *CheckCppJobExecutor) SupportsType(jobType job.Type) bool {
	return jobType == job.CheckCpp
}

func (e *CheckCppJobExecutor) Execute(ctx context.Context, jb jobs.Job) results.Result {
	errorResult := func(err error) results.Result {
		return results.NewCheckResultErr(jb.GetID(), err.Error())
	}

	if jb.GetType() != job.CheckCpp {
		return errorResult(fmt.Errorf("unsupported job type %s for %s executor", jb.GetType(), job.CheckCpp))
	}
	checkCppJob := jb.AsCheckCpp()

	compiledChecker, unlock, err := e.sourceProvider.Locate(ctx, checkCppJob.CompiledChecker.SourceID)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate compiled_checker input: %w", err))
	}
	defer unlock()

	correctOutput, unlock, err := e.sourceProvider.Locate(ctx, checkCppJob.CorrectOutput.SourceID)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate correct_output input: %w", err))
	}
	defer unlock()

	suspectOutput, unlock, err := e.sourceProvider.Locate(ctx, checkCppJob.SuspectOutput.SourceID)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate suspect_output input: %w", err))
	}
	defer unlock()

	const compiledCheckerMountPath = "/a.out"
	const correctOutputMountPath = "/correct.txt"
	const suspectOutputMountPath = "/suspect.txt"

	checkVerdictReader := bytes.NewBuffer(nil)
	stderr := bytes.NewBuffer(nil)
	err = e.runtime.Execute(ctx, []string{compiledCheckerMountPath, correctOutputMountPath, suspectOutputMountPath}, runtime.ExecuteParams{
		InFiles: []runtime.File{
			{InsideLocation: correctOutputMountPath, OutsideLocation: correctOutput},
			{InsideLocation: suspectOutputMountPath, OutsideLocation: suspectOutput},
			{InsideLocation: compiledCheckerMountPath, OutsideLocation: compiledChecker},
		},
		Stdout: checkVerdictReader,
		Stderr: stderr,
	})
	if err != nil {
		e.log.Error("execute checker in runtime error", slog.Any("err", err))
		return errorResult(err)
	}

	e.log.Info("command ok")

	checkVerdictOutput, err := io.ReadAll(checkVerdictReader)
	if err != nil {
		return errorResult(fmt.Errorf("failed to read check_verdict output: %w", err))
	}

	if string(checkVerdictOutput) == string(job.StatusOK) {
		return results.NewCheckResultOK(jb.GetID())
	}
	if string(checkVerdictOutput) == string(job.StatusWA) {
		return results.NewCheckResultWA(jb.GetID())
	}
	return errorResult(fmt.Errorf("failed to parse check_verdict output: %s", string(checkVerdictOutput)))
}
