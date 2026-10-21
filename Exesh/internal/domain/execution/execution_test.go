package execution

import (
	"encoding/json"
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/output"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source"
	"strings"
	"testing"
)

func TestLinearJobsStaySeparate(t *testing.T) {
	compileID := testJobID(t, "a")
	runID := testJobID(t, "b")
	codeID := testSourceID(t, "c")
	runInputID := testSourceID(t, "d")
	compiledCodeID := testSourceID(t, "a")

	compile := jobs.NewCompileCppJob(
		compileID, job.StatusOK, 1000, 256, 500, 128,
		input.NewInput(input.Inline, codeID), output.NewOutput("bin"),
	)
	run := jobs.NewRunCppJob(
		runID, job.StatusOK, 1000, 256, 500, 128,
		input.NewInput(input.Artifact, compiledCodeID),
		input.NewInput(input.Inline, runInputID), output.NewOutput("output"), false,
	)

	for _, tc := range []struct {
		name          string
		compileStatus job.Status
		wantRun       bool
	}{
		{name: "compile succeeds", compileStatus: job.StatusOK, wantRun: true},
		{name: "compile fails", compileStatus: job.StatusCE, wantRun: false},
	} {
		t.Run(tc.name, func(t *testing.T) {
			ex := NewExecution(Definition{})
			ex.Stages = []*Stage{{Name: "test", Jobs: []jobs.Job{compile, run}}}
			ex.BuildGraph()

			if got := len(ex.Stages[0].Jobs); got != 2 {
				t.Fatalf("graph has %d jobs, want both compile and run", got)
			}
			first := ex.PickJobs()
			if len(first) != 1 || first[0].GetID() != compileID || first[0].GetType() != job.CompileCpp {
				t.Fatalf("first dispatch = %#v, want standalone compile", first)
			}
			ex.DoneJob(compileID, tc.compileStatus)
			second := ex.PickJobs()
			if tc.wantRun {
				if len(second) != 1 || second[0].GetID() != runID || second[0].GetType() != job.RunCpp {
					t.Fatalf("second dispatch = %#v, want standalone run", second)
				}
				if deps := second[0].GetDependencies(); len(deps) != 1 || deps[0] != compileID {
					t.Fatalf("run dependencies = %v, want compile artifact", deps)
				}
				ex.DoneJob(runID, job.StatusOK)
				_ = ex.PickJobs()
			} else if len(second) != 0 {
				t.Fatalf("run dispatched after compile failure: %#v", second)
			}
			if !ex.IsDone() {
				t.Fatal("execution did not finish")
			}
		})
	}
}

func TestChainPayloadsAreRejected(t *testing.T) {
	var jb jobs.Job
	if err := json.Unmarshal([]byte(`{"type":"chain"}`), &jb); err == nil {
		t.Fatal("chain job was accepted")
	}
	var res results.Result
	if err := json.Unmarshal([]byte(`{"type":"chain"}`), &res); err == nil {
		t.Fatal("chain result was accepted")
	}
}

func testJobID(t *testing.T, digit string) job.ID {
	t.Helper()
	var id job.ID
	if err := id.FromString(strings.Repeat(digit, 40)); err != nil {
		t.Fatal(err)
	}
	return id
}

func testSourceID(t *testing.T, digit string) source.ID {
	t.Helper()
	var id source.ID
	if err := id.FromString(strings.Repeat(digit, 40)); err != nil {
		t.Fatal(err)
	}
	return id
}
