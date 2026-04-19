package executors

import (
	"context"
	"errors"
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/executor"
	"exesh/internal/runtime"
	"fmt"
	errs "github.com/DIvanCode/filestorage/pkg/errors"
	"log/slog"
)

type ChainJobExecutor struct {
	log *slog.Logger

	runtimeFactory       *runtime.JobRuntimeFactory
	innerExecutorFactory *executor.ExecutorFactory
	job                  jobs.Job

	innerExecutors          []executor.JobExecutor
	runtimeResourceRegistry *executor.RuntimeResourceRegistry

	runtime        runtime.Runtime
	sourceProvider sourceProvider
}

type ChainExecutorFactory struct {
	log            *slog.Logger
	sourceProvider sourceProvider

	runtimeFactory       *runtime.JobRuntimeFactory
	innerExecutorFactory *executor.ExecutorFactory
}

func NewChainExecutorFactory(
	log *slog.Logger,
	sourceProvider sourceProvider,
	runtimeFactory *runtime.JobRuntimeFactory,
	innerExecutorFactory *executor.ExecutorFactory,
) *ChainExecutorFactory {
	return &ChainExecutorFactory{
		log:                  log,
		sourceProvider:       sourceProvider,
		runtimeFactory:       runtimeFactory,
		innerExecutorFactory: innerExecutorFactory,
	}
}

func (f *ChainExecutorFactory) SupportsType(jobType job.Type) bool {
	return jobType == job.Chain
}

func (f *ChainExecutorFactory) Create(jb jobs.Job) (executor.JobExecutor, error) {
	if jb.GetType() != job.Chain {
		return nil, fmt.Errorf("unsupported job type %s for %s executor", jb.GetType(), job.Chain)
	}

	return &ChainJobExecutor{
		log:                  f.log,
		runtimeFactory:       f.runtimeFactory,
		innerExecutorFactory: f.innerExecutorFactory,
		sourceProvider:       f.sourceProvider,
		job:                  jb,
	}, nil
}

func (f *ChainExecutorFactory) CreateWithRuntime(
	jb jobs.Job,
	_ runtime.Runtime,
	_ *executor.RuntimeResourceRegistry,
) (executor.JobExecutor, error) {
	return f.Create(jb)
}

func (e *ChainJobExecutor) Init(ctx context.Context) error {
	chainJob := e.job.AsChain()
	if len(chainJob.Jobs) == 0 {
		return fmt.Errorf("empty chain")
	}

	e.innerExecutors = make([]executor.JobExecutor, 0, len(chainJob.Jobs))
	e.runtimeResourceRegistry = executor.NewRuntimeResourceRegistry(len(chainJob.Jobs) * 2)
	rt, err := e.runtimeFactory.Create(ctx, chainJob.Jobs[0])
	if err != nil {
		return fmt.Errorf("create chain runtime: %w", err)
	}
	e.runtime = rt

	for _, innerJob := range chainJob.Jobs {
		innerExec, err := e.innerExecutorFactory.CreateWithRuntime(innerJob, e.runtime, e.runtimeResourceRegistry)
		if err != nil {
			return fmt.Errorf("create inner executor: %w", err)
		}
		e.innerExecutors = append(e.innerExecutors, innerExec)

		if err = innerExec.Init(ctx); err != nil {
			return fmt.Errorf("init inner executor: %w", err)
		}
	}

	return nil
}

func (e *ChainJobExecutor) PrepareInput(ctx context.Context) error {
	chainJob := e.job.AsChain()
	if len(chainJob.Jobs) == 0 || len(e.innerExecutors) == 0 {
		return fmt.Errorf("empty chain")
	}

	prepareSingleInput := func(in input.Input) error {
		srcPath, unlock, locateErr := e.sourceProvider.Locate(ctx, in.SourceID)
		if locateErr != nil {
			return fmt.Errorf("locate source %s: %w", in.SourceID.String(), locateErr)
		}
		defer unlock()

		runtimePath, err := e.runtimeResourceRegistry.Get(in.SourceID)
		if err != nil {
			return fmt.Errorf("get runtime path %s: %w", in.SourceID.String(), err)
		}

		if err := e.runtime.CopyToRuntime(ctx, srcPath, runtimePath); err != nil {
			return fmt.Errorf("copy source %s to runtime: %w", in.SourceID.String(), err)
		}

		return nil
	}

	for _, in := range chainJob.GetInputs() {
		if err := prepareSingleInput(in); err != nil {
			return fmt.Errorf("failed to prepare input %s: %w", in.SourceID.String(), err)
		}
	}

	return nil
}

func (e *ChainJobExecutor) ExecuteCommand(ctx context.Context) results.Result {
	chainJob := e.job.AsChain()
	if len(chainJob.Jobs) == 0 {
		return results.NewChainResultErr(chainJob.GetID(), "empty chain", nil)
	}
	if len(e.innerExecutors) == 0 {
		return results.NewChainResultErr(chainJob.GetID(), "chain is not initialized", nil)
	}

	innerResults := make([]results.Result, 0, len(chainJob.Jobs))

	for i, innerJob := range chainJob.Jobs {
		innerExec := e.innerExecutors[i]

		innerResult := innerExec.ExecuteCommand(ctx)
		innerResults = append(innerResults, innerResult)

		if innerResult.GetError() != nil {
			return results.NewChainResultErr(chainJob.GetID(), innerResult.GetError().Error(), innerResults)
		}
		if innerResult.GetStatus() != innerJob.GetSuccessStatus() {
			return results.NewChainResult(chainJob.GetID(), innerResult.GetStatus(), innerResults)
		}
	}

	return results.NewChainResult(chainJob.GetID(), innerResults[len(innerResults)-1].GetStatus(), innerResults)
}

func (e *ChainJobExecutor) SaveOutput(ctx context.Context) error {
	chainJob := e.job.AsChain()
	if len(chainJob.Jobs) == 0 || len(e.innerExecutors) == 0 {
		return nil
	}

	lastIdx := len(e.innerExecutors) - 1
	if err := e.innerExecutors[lastIdx].SaveOutput(ctx); err != nil {
		if errors.Is(err, errs.ErrFileAlreadyExists) {
			return nil
		}
		return err
	}

	return nil
}

func (e *ChainJobExecutor) Stop(ctx context.Context) error {
	if e.runtime == nil {
		return nil
	}
	return e.runtime.Stop(ctx)
}
