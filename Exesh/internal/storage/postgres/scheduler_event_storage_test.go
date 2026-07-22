package postgres

import (
	"context"
	"database/sql"
	"testing"
	"time"
)

type recordingSQLExecutor struct {
	queries []string
	args    [][]any
}

func (e *recordingSQLExecutor) ExecContext(_ context.Context, query string, args ...any) (sql.Result, error) {
	e.queries = append(e.queries, query)
	e.args = append(e.args, args)
	return nil, nil
}

func TestDeleteOldSchedulerEventsExecutesOneStatementPerCall(t *testing.T) {
	cutoff := time.Date(2026, time.July, 15, 12, 0, 0, 0, time.UTC)
	db := &recordingSQLExecutor{}

	if err := deleteOldSchedulerEvents(context.Background(), db, cutoff); err != nil {
		t.Fatalf("delete old scheduler events: %v", err)
	}

	if len(db.queries) != len(deleteOldSchedulerEventQueries) {
		t.Fatalf("executed %d statements, want %d", len(db.queries), len(deleteOldSchedulerEventQueries))
	}
	for i, statement := range deleteOldSchedulerEventQueries {
		if db.queries[i] != statement.query {
			t.Errorf("query %d = %q, want %q", i, db.queries[i], statement.query)
		}
		if len(db.args[i]) != 1 || db.args[i][0] != cutoff {
			t.Errorf("query %d args = %#v, want cutoff %v", i, db.args[i], cutoff)
		}
	}
}
