package postgres

import (
	"context"
	"database/sql"
	"fmt"
	"os"
	"testing"
	"time"
)

func migrationTestTransaction(t *testing.T) (context.Context, *sql.Tx) {
	t.Helper()
	dsn := os.Getenv("EXESH_TEST_POSTGRES_DSN")
	if dsn == "" {
		t.Skip("EXESH_TEST_POSTGRES_DSN is required for the PostgreSQL migration tests")
	}
	db, err := sql.Open("pgx", dsn)
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = db.Close() })
	ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
	t.Cleanup(cancel)
	tx, err := db.BeginTx(ctx, nil)
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = tx.Rollback() })
	// Keep every test isolated and roll back both fixtures and migration writes.
	schema := fmt.Sprintf("migration_test_%d", time.Now().UnixNano())
	if _, err := tx.ExecContext(ctx, "CREATE SCHEMA "+schema+"; SET LOCAL search_path TO "+schema); err != nil {
		t.Fatal(err)
	}
	return withTx(ctx, tx), tx
}

func assertTableExists(t *testing.T, ctx context.Context, tx *sql.Tx, table string, exists bool) {
	t.Helper()
	var found bool
	if err := tx.QueryRowContext(ctx, "SELECT to_regclass($1) IS NOT NULL", table).Scan(&found); err != nil {
		t.Fatal(err)
	} else if found != exists {
		t.Fatalf("table/index %s exists=%v, want %v", table, found, exists)
	}
}

func TestMigrateCreatesCompleteSchema(t *testing.T) {
	ctx, tx := migrationTestTransaction(t)
	for range 2 {
		if err := Migrate(ctx); err != nil {
			t.Fatal(err)
		}
	}
	for _, table := range []string{
		"executions", "messages", "outbox", "category_time_histogram", "category_memory_histogram",
		"idx_category_time_histogram_category", "idx_category_memory_histogram_category",
	} {
		assertTableExists(t, ctx, tx, table, true)
	}
	for _, table := range []string{"exesh_execution_events", "exesh_job_events", "exesh_worker_events"} {
		assertTableExists(t, ctx, tx, table, false)
	}
	if _, err := tx.ExecContext(ctx, `
		INSERT INTO Executions(id, stages, sources, status) VALUES ('new-id', '[]', '[]', 'new');
		INSERT INTO Messages(execution_id, message_id, message, created_at)
		VALUES ('new-id', 1, '{}', now());
		INSERT INTO Outbox(message) VALUES ('new-message');
		INSERT INTO category_time_histogram(category_name, time_bucket_ms, cnt)
		VALUES ('test', 0, 1);
		INSERT INTO category_memory_histogram(category_name, memory_bucket_mb, cnt)
		VALUES ('test', 0, 1);
	`); err != nil {
		t.Fatalf("new schema cannot store regular Exesh data: %v", err)
	}
}

func TestMigrateUpgradesExistingDataAndRollsBackOnDependency(t *testing.T) {
	ctx, tx := migrationTestTransaction(t)
	// This is the historical execution schema before weight and tries existed.
	if _, err := tx.ExecContext(ctx, `
		CREATE TABLE Executions (
			id varchar(36) PRIMARY KEY, stages jsonb, sources jsonb,
			status varchar(32), created_at timestamp, scheduled_at timestamp, finished_at timestamp
		);
		INSERT INTO Executions(id, stages, sources, status) VALUES ('old-id', '[]', '[]', 'new');
		CREATE TABLE exesh_execution_events (id integer PRIMARY KEY);
		CREATE TABLE exesh_job_events (id integer PRIMARY KEY);
		CREATE TABLE exesh_worker_events (id integer PRIMARY KEY);
		INSERT INTO exesh_execution_events VALUES (1);
		INSERT INTO exesh_job_events VALUES (1);
		INSERT INTO exesh_worker_events VALUES (1);
		CREATE TABLE category_time_histogram (
			category_name text NOT NULL, time_bucket_ms integer NOT NULL, cnt bigint NOT NULL,
			PRIMARY KEY (category_name, time_bucket_ms)
		);
		INSERT INTO category_time_histogram VALUES ('old', 50, 7);
		CREATE TABLE category_memory_histogram (
			category_name text NOT NULL, memory_bucket_mb integer NOT NULL, cnt bigint NOT NULL,
			PRIMARY KEY (category_name, memory_bucket_mb)
		);
		INSERT INTO category_memory_histogram VALUES ('old', 16, 5);
		CREATE VIEW event_consumer AS SELECT * FROM exesh_worker_events;
		SAVEPOINT before_cleanup;
	`); err != nil {
		t.Fatal(err)
	}
	// A dependent object must block the cleanup without CASCADE. The failed
	// startup transaction can be rolled back without changing the old schema.
	if err := Migrate(ctx); err == nil {
		t.Fatal("migration unexpectedly removed a dependent view")
	}
	if _, err := tx.ExecContext(ctx, "ROLLBACK TO SAVEPOINT before_cleanup"); err != nil {
		t.Fatal(err)
	}
	assertTableExists(t, ctx, tx, "exesh_worker_events", true)
	var oldColumnExists bool
	if err := tx.QueryRowContext(ctx, `
		SELECT EXISTS (
			SELECT 1 FROM information_schema.columns
			WHERE table_schema = current_schema() AND table_name = 'executions' AND column_name = 'weight'
		)
	`).Scan(&oldColumnExists); err != nil || oldColumnExists {
		t.Fatalf("failed migration changed execution schema: weight=%v err=%v", oldColumnExists, err)
	}
	if _, err := tx.ExecContext(ctx, "DROP VIEW event_consumer"); err != nil {
		t.Fatal(err)
	}
	for range 2 {
		if err := Migrate(ctx); err != nil {
			t.Fatal(err)
		}
	}
	for _, table := range []string{"exesh_execution_events", "exesh_job_events", "exesh_worker_events"} {
		assertTableExists(t, ctx, tx, table, false)
	}
	var weight int64
	var tries int
	if err := tx.QueryRowContext(ctx, "SELECT weight, tries FROM Executions WHERE id = 'old-id'").Scan(&weight, &tries); err != nil || weight != 0 || tries != 0 {
		t.Fatalf("old execution was lost or changed: weight=%d tries=%d err=%v", weight, tries, err)
	}
	for _, sample := range []struct {
		query string
		want  int
	}{
		{"SELECT cnt FROM category_time_histogram WHERE category_name = 'old' AND time_bucket_ms = 50", 7},
		{"SELECT cnt FROM category_memory_histogram WHERE category_name = 'old' AND memory_bucket_mb = 16", 5},
	} {
		var count int
		if err := tx.QueryRowContext(ctx, sample.query).Scan(&count); err != nil || count != sample.want {
			t.Fatalf("existing histogram changed: count=%d want=%d err=%v", count, sample.want, err)
		}
	}
}
