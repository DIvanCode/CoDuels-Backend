package executors

import (
	"bytes"
	"context"
	"errors"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"fmt"
	"io"
	"log/slog"
	"time"

	"exesh/internal/domain/execution/result/results"
	"exesh/internal/runtime"
)

type RunGoJobExecutor struct {
	log            *slog.Logger
	sourceProvider sourceProvider
	outputProvider outputProvider
	runtime        runtime.Runtime
}

func NewRunGoJobExecutor(log *slog.Logger, sourceProvider sourceProvider, outputProvider outputProvider, rt runtime.Runtime) *RunGoJobExecutor {
	return &RunGoJobExecutor{
		log:            log,
		sourceProvider: sourceProvider,
		outputProvider: outputProvider,
		runtime:        rt,
	}
}

func (e *RunGoJobExecutor) SupportsType(jobType job.Type) bool {
	return jobType == job.RunGo
}

func (e *RunGoJobExecutor) Execute(ctx context.Context, jb jobs.Job) results.Result {
	errorResult := func(err error) results.Result {
		return results.NewRunResultErr(jb.GetID(), err.Error())
	}

	if jb.GetType() != job.RunGo {
		return errorResult(fmt.Errorf("unsupported job type %s for %s executor", jb.GetType(), job.RunGo))
	}
	runGoJob := jb.AsRunGo()

	compiledCode, unlock, err := e.sourceProvider.Locate(ctx, runGoJob.CompiledCode.SourceID)
	if err != nil {
		return errorResult(fmt.Errorf("failed to locate compiled_code input: %w", err))
	}
	defer unlock()

	runInput, unlock, err := e.sourceProvider.Read(ctx, runGoJob.RunInput.SourceID)
	if err != nil {
		return errorResult(fmt.Errorf("failed to read run_input input: %w", err))
	}
	defer unlock()

	runOutput, commitOutput, abortOutput, err := e.outputProvider.Create(ctx, jb.GetID(), runGoJob.RunOutput.File)
	if err != nil {
		return errorResult(fmt.Errorf("failed to create run_output output: %w", err))
	}
	commit := func() error {
		if err = commitOutput(); err != nil {
			_ = abortOutput()
			return fmt.Errorf("failed to commit run_output output: %w", err)
		}
		abortOutput = func() error { return nil }
		return nil
	}
	defer func() {
		_ = abortOutput()
	}()

	const compiledCodeMountPath = "/a.out"

	stderr := bytes.NewBuffer(nil)
	err = e.runtime.Execute(ctx,
		[]string{compiledCodeMountPath},
		runtime.ExecuteParams{
			// TODO: Limits
			Limits: runtime.Limits{
				Memory: runtime.MemoryLimit(int64(runGoJob.MemoryLimit) * int64(runtime.Megabyte)),
				Time:   runtime.TimeLimit(int64(runGoJob.TimeLimit) * int64(time.Millisecond)),
			},
			InFiles: []runtime.File{{OutsideLocation: compiledCode, InsideLocation: compiledCodeMountPath}},
			Stderr:  stderr,
			Stdin:   runInput,
			Stdout:  runOutput,
		})
	if err != nil {
		e.log.Error("execute binary in runtime error", slog.Any("err", err))
		if errors.Is(err, runtime.ErrTimeout) {
			return results.NewRunResultTL(jb.GetID())
		}
		if errors.Is(err, runtime.ErrOutOfMemory) {
			return results.NewRunResultML(jb.GetID())
		}
		return results.NewRunResultRE(jb.GetID())
	}

	e.log.Info("command ok")

	if err = commit(); err != nil {
		return errorResult(fmt.Errorf("failed to commit output creation: %w", err))
	}

	if !runGoJob.ShowOutput {
		return results.NewRunResultOK(jb.GetID())
	}

	runOutputReader, unlock, err := e.outputProvider.Read(ctx, jb.GetID(), runGoJob.RunOutput.File)
	if err != nil {
		return errorResult(fmt.Errorf("failed to open run output: %w", err))
	}
	defer unlock()

	out, err := io.ReadAll(runOutputReader)
	if err != nil {
		return errorResult(fmt.Errorf("failed to read run output: %w", err))
	}

	return results.NewRunResultWithOutput(jb.GetID(), string(out))
}
