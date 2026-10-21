package executors

import (
	"context"
	"errors"
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/source"
	"exesh/internal/runtime"
	"testing"
)

type lockCheckingSourceProvider struct {
	active int
	calls  int
}

func (p *lockCheckingSourceProvider) Locate(_ context.Context, _ source.ID) (string, func(), error) {
	if p.active != 0 {
		return "", nil, errors.New("previous source lock is still held")
	}
	p.active++
	p.calls++
	return "source", func() { p.active-- }, nil
}

type inputCopyRuntime struct {
	provider *lockCheckingSourceProvider
	copies   int
	failAt   int
}

func (r *inputCopyRuntime) Init(context.Context) error                            { return nil }
func (r *inputCopyRuntime) Stop(context.Context) error                            { return nil }
func (r *inputCopyRuntime) CopyFromRuntime(context.Context, string, string) error { return nil }
func (r *inputCopyRuntime) RunCommand(context.Context, []string, runtime.RunParams) (*runtime.Usage, error) {
	return nil, nil
}
func (r *inputCopyRuntime) CopyToRuntime(_ context.Context, _, _ string) error {
	if r.provider.active != 1 {
		return errors.New("source lock was released before copy")
	}
	r.copies++
	if r.copies == r.failAt {
		return errors.New("copy failed")
	}
	return nil
}

func TestCheckCppCopiesInputsWithoutOverlappingSourceLocks(t *testing.T) {
	for _, tc := range []struct {
		name       string
		failAt     int
		wantCopies int
		wantError  bool
	}{
		{name: "all inputs", wantCopies: 4},
		{name: "copy failure", failAt: 2, wantCopies: 2, wantError: true},
	} {
		t.Run(tc.name, func(t *testing.T) {
			provider := &lockCheckingSourceProvider{}
			rt := &inputCopyRuntime{provider: provider, failAt: tc.failAt}
			in := input.NewInput(input.Inline, source.ID{})
			jb := jobs.NewCheckCppJob(job.ID{}, job.StatusOK, 1000, 256, 500, 128, in, in, in, in)
			exec := &CheckCppJobExecutor{sourceProvider: provider, runtime: rt, job: jb}

			err := exec.PrepareInput(context.Background())
			if (err != nil) != tc.wantError {
				t.Fatalf("PrepareInput error = %v, wantError=%v", err, tc.wantError)
			}
			if provider.active != 0 || provider.calls != tc.wantCopies || rt.copies != tc.wantCopies {
				t.Fatalf("active=%d locate=%d copies=%d, want released locks and %d copies", provider.active, provider.calls, rt.copies, tc.wantCopies)
			}
		})
	}
}
