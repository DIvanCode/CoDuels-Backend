package strategies

import (
	"testing"

	"taski/internal/domain/testing/execution"
	"taski/internal/domain/testing/job"
	"taski/internal/domain/testing/job/jobs"
	"taski/internal/domain/testing/strategy"
)

func TestWriteCodeVerdictAfterEarlierTestCompletes(t *testing.T) {
	runSeven := job.Name("run suspect code on test 7")
	checkSix := job.Name("check suspect on test 6")
	ts := &WriteCodeTaskTestingStrategy{
		Details: strategy.Details{Stages: execution.Stages{{Jobs: []jobs.Job{
			{IJob: &job.Details{Name: runSeven, SuccessStatus: job.StatusOK}},
			{IJob: &job.Details{Name: checkSix, SuccessStatus: job.StatusOK}},
		}}}},
		TestsCount: 10,
		TestStatus: map[int]job.Status{
			1: job.StatusOK, 2: job.StatusOK, 3: job.StatusOK,
			4: job.StatusOK, 5: job.StatusOK,
		},
	}

	ts.UpdateJobStatus(runSeven, job.StatusRE, nil)
	if ts.Verdict != nil {
		t.Fatalf("verdict before test 6 = %q, want undetermined", *ts.Verdict)
	}

	ts.UpdateJobStatus(checkSix, job.StatusOK, nil)
	if got, want := ts.GetVerdict(), "Runtime Error on test 7"; got != want {
		t.Fatalf("verdict after test 6 = %q, want %q", got, want)
	}

	ts.UpdateJobStatus(checkSix, job.StatusWA, nil)
	if got, want := ts.GetVerdict(), "Runtime Error on test 7"; got != want {
		t.Fatalf("verdict after late duplicate = %q, want %q", got, want)
	}
}

func TestWriteCodeCalculateVerdictUsesFirstKnownFailure(t *testing.T) {
	ts := &WriteCodeTaskTestingStrategy{
		TestsCount: 10,
		TestStatus: map[int]job.Status{
			1: job.StatusOK, 2: job.StatusOK, 3: job.StatusOK,
			4: job.StatusOK, 5: job.StatusOK, 7: job.StatusRE,
		},
	}

	ts.CalculateVerdict()
	if ts.Verdict != nil {
		t.Fatal("calculated a verdict while test 6 is unknown")
	}

	ts.TestStatus[6] = job.StatusWA
	ts.CalculateVerdict()
	if got, want := ts.GetVerdict(), "Wrong Answer on test 6"; got != want {
		t.Fatalf("verdict = %q, want %q", got, want)
	}
}

func TestWriteCodeCalculateVerdictAcceptsAllPassedTests(t *testing.T) {
	ts := &WriteCodeTaskTestingStrategy{
		TestsCount: 2,
		TestStatus: map[int]job.Status{1: job.StatusOK, 2: job.StatusOK},
	}

	ts.CalculateVerdict()
	if got := ts.GetVerdict(); got != strategy.AcceptedVerdict {
		t.Fatalf("verdict = %q, want %q", got, strategy.AcceptedVerdict)
	}
}
