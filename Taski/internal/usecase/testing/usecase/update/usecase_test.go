package update

import (
	"encoding/json"
	"testing"
	"time"

	"taski/internal/domain/task"
	domain "taski/internal/domain/testing"
	"taski/internal/domain/testing/event"
	"taski/internal/domain/testing/event/events"
	"taski/internal/domain/testing/job"
	"taski/internal/domain/testing/message/messages"
	"taski/internal/domain/testing/strategy"
	"taski/internal/domain/testing/strategy/strategies"
)

func TestFinishRecalculatesPersistedWriteCodeVerdict(t *testing.T) {
	ts := &strategies.WriteCodeTaskTestingStrategy{
		Details:    strategy.Details{TaskType: task.WriteCode},
		TestsCount: 10,
		TestStatus: map[int]job.Status{
			1: job.StatusOK, 2: job.StatusOK, 3: job.StatusOK,
			4: job.StatusOK, 5: job.StatusOK, 6: job.StatusOK, 7: job.StatusRE,
		},
	}
	serialized, err := json.Marshal(strategies.TestingStrategy{ITestingStrategy: ts})
	if err != nil {
		t.Fatal(err)
	}
	var restored strategies.TestingStrategy
	if err := json.Unmarshal(serialized, &restored); err != nil {
		t.Fatal(err)
	}
	sol := domain.Solution{
		TestingStrategy: restored,
	}
	finish := events.Event{IEvent: &events.FinishExecutionEvent{
		Details: event.Details{Type: event.FinishExecution},
	}}
	now := time.Now()

	msg, sent, err := (&UseCase{}).handleExecutionEvent(&sol, finish, now)
	if err != nil || !sent {
		t.Fatalf("finish result: sent=%t, err=%v", sent, err)
	}
	if got, want := msg.IMessage.(*messages.FinishTestingMessage).Verdict, "Runtime Error on test 7"; got != want {
		t.Fatalf("finish verdict = %q, want %q", got, want)
	}
	if sol.FinishedAt == nil || !sol.FinishedAt.Equal(now) {
		t.Fatal("solution was not marked finished")
	}
}
