//go:build ignore

package main

import (
	"context"
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/input/inputs"
	"exesh/internal/domain/execution/job"
	jobs2 "exesh/internal/domain/execution/job/jobs"
	"exesh/internal/executor/executors"
	"exesh/internal/runtime/docker"
	"fmt"
	"io"
	"log/slog"
	"os"
	// "time"
)

type dummyInputProvider struct{}

func (dp *dummyInputProvider) Create(ctx context.Context, in input.Input) (w io.Writer, commit, abort func() error, err error) {
	commit = func() error { return nil }
	abort = func() error { return nil }
	f, err := os.OpenFile(in.GetFile(), os.O_CREATE|os.O_RDWR, 0o755)
	if err != nil {
		err = fmt.Errorf("open file: %w", err)
		return f, commit, abort, err
	}
	commit = f.Close
	return f, commit, abort, err
}

func (dp *dummyInputProvider) Locate(ctx context.Context, in input.Input) (path string, unlock func(), err error) {
	unlock = func() {}
	return in.GetFile(), func() {}, nil
}

func (dp *dummyInputProvider) Read(ctx context.Context, in input.Input) (r io.Reader, unlock func(), err error) {
	unlock = func() {}
	f, err := os.OpenFile(in.GetFile(), os.O_RDONLY, 0o755)
	if err != nil {
		err = fmt.Errorf("open file: %w", err)
		return f, unlock, err
	}
	return f, unlock, err
}

type dummyOutputProvider struct{}

func (dp *dummyOutputProvider) Create(ctx context.Context, out output.Output) (w io.Writer, commit, abort func() error, err error) {
	commit = func() error { return nil }
	abort = func() error { return nil }
	f, err := os.OpenFile(out.GetFile(), os.O_CREATE|os.O_RDWR, 0o755)
	if err != nil {
		err = fmt.Errorf("open file: %w", err)
		return f, commit, abort, err
	}
	commit = f.Close
	return f, commit, abort, err
}

func (dp *dummyOutputProvider) Locate(ctx context.Context, out output.Output) (path string, unlock func(), err error) {
	unlock = func() {}
	return out.GetFile(), func() {}, nil
}

func (dp *dummyOutputProvider) Reserve(ctx context.Context, out output.Output) (path string, unlock func() error, smth func() error, err error) {
	return out.GetFile(), func() error { return nil }, func() error { return nil }, nil
}

func (dp *dummyOutputProvider) Read(ctx context.Context, out output.Output) (r io.Reader, unlock func(), err error) {
	unlock = func() {}
	f, err := os.OpenFile(out.GetFile(), os.O_RDONLY, 0o755)
	if err != nil {
		err = fmt.Errorf("open file: %w", err)
		return f, unlock, err
	}
	return f, unlock, err
}

func Unref[T any](t *T) T {
	return *t
}

func Ref[T any](t T) *T {
	return &t
}

func main() {
	compileJobId := job.JobID([]byte("1234567890123456789012345678901234567890"))
	runJobId := job.JobID([]byte("0123456789012345678901234567890123456789"))
	checkJobId := job.JobID([]byte("9012345678901234567890123456789012345678"))
	workerID := "worker-id"
	rt, err := docker.New(
		docker.WithDefaultClient(),
		docker.WithBaseImage("gcc"),
		docker.WithRestrictivePolicy(),
	)
	if err != nil {
		panic(err)
	}
	compileExecutor := executors.NewCompileCppJobExecutor(slog.Default(), &dummyInputProvider{}, &dummyOutputProvider{}, rt)

	compilationResult := compileExecutor.Execute(context.Background(), Ref(jobs2.NewCompileCppJob(
		compileJobId,
		inputs.NewArtifactInput("main.cpp", compileJobId),
		output.NewArtifactOutput("a.out", compileJobId),
	)))
	fmt.Printf("compile: %#v\n", compilationResult)

	checkerCompilationResult := compileExecutor.Execute(context.Background(), Ref(jobs2.NewCompileCppJob(
		compileJobId,
		inputs.NewArtifactInput("checker.cpp", compileJobId),
		output.NewArtifactOutput("a.checker.out", compileJobId),
	)))
	fmt.Printf("compile checker: %#v\n", checkerCompilationResult)

	runExecutor := executors.NewRunCppJobExecutor(slog.Default(), &dummyInputProvider{}, &dummyOutputProvider{}, rt)
	runResult := runExecutor.Execute(context.Background(), Ref(jobs2.NewRunCppJob(
		runJobId, inputs.NewArtifactInput("a.out", runJobId),
		inputs.NewArtifactInput("in.txt", runJobId),
		output.NewArtifactOutput("out.txt", runJobId), 0, 0, true)))
	fmt.Printf("run: %#v\n", runResult)

	checkExecutor := executors.NewCheckCppJobExecutor(slog.Default(), &dummyInputProvider{}, &dummyOutputProvider{}, rt)
	checkResult := checkExecutor.Execute(context.Background(), Ref(jobs2.NewCheckCppJob(
		runJobId, inputs.NewArtifactInput("a.checker.out", checkJobId),
		inputs.NewArtifactInput("correct.txt", checkJobId),
		inputs.NewArtifactInput("out.txt", checkJobId),
	)))
	fmt.Printf("check: %#v\n", checkResult)
}
