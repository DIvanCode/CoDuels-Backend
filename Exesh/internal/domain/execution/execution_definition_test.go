package execution

import (
	"testing"
	"time"
)

func TestRefreshScheduledAtDoesNotCountJobCompletionAsRetry(t *testing.T) {
	def := NewExecutionDefinition(nil, nil, 1)
	first := time.Now()
	def.SetScheduled(first)
	if def.Tries != 1 {
		t.Fatalf("initial attempts = %d, want 1", def.Tries)
	}

	progress := first.Add(time.Second)
	def.RefreshScheduledAt(progress)
	if def.Tries != 1 || def.ScheduledAt == nil || !def.ScheduledAt.Equal(progress) {
		t.Fatalf("progress changed attempt count or failed to refresh timestamp: %+v", def)
	}

	retry := progress.Add(time.Second)
	def.SetScheduled(retry)
	if def.Tries != 2 || def.ScheduledAt == nil || !def.ScheduledAt.Equal(retry) {
		t.Fatalf("replay did not count exactly one new attempt: %+v", def)
	}
}
