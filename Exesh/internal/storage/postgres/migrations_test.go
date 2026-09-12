package postgres

import (
	"context"
	"database/sql"
	"fmt"
	"os"
	"testing"
	"time"
)

func TestMigrateRetiresOnlyDashboardTables(t *testing.T) {
	dsn := os.Getenv("EXESH_TEST_POSTGRES_DSN")
	if dsn == "" {
		t.Skip("EXESH_TEST_POSTGRES_DSN is required for the PostgreSQL migration test")
	}
	db, err := sql.Open("pgx", dsn)
	if err != nil {
		t.Fatal(err)
	}
	defer db.Close()
	ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
	defer cancel()
	tx, err := db.BeginTx(ctx, nil)
	if err != nil {
		t.Fatal(err)
	}
	defer tx.Rollback()
	// All fixtures and migration runs stay inside a rolled-back, isolated schema.
	schema := fmt.Sprintf("migration_test_%d", time.Now().UnixNano())
	if _, err = tx.ExecContext(ctx, "CREATE SCHEMA "+schema+"; SET LOCAL search_path TO "+schema); err != nil {
		t.Fatal(err)
	}
	ctx = withTx(ctx, tx)
	legacy := []string{"exesh_execution_events", "exesh_job_events", "exesh_worker_events"}
	retained := []string{"executions", "messages", "outbox", "category_time_histogram", "category_memory_histogram"}
	for _, table := range append(legacy, retained...) {
		if _, err = tx.ExecContext(ctx, "CREATE TABLE "+table+" (id integer PRIMARY KEY); INSERT INTO "+table+" VALUES (42)"); err != nil {
			t.Fatal(err)
		}
	}
	// Refuse to cascade into an unexpected dependent object. PostgreSQL rolls
	// back the failed drop so no partial cleanup can be committed.
	if _, err = tx.ExecContext(ctx, "CREATE VIEW event_consumer AS SELECT * FROM exesh_worker_events; SAVEPOINT before_cleanup"); err != nil {
		t.Fatal(err)
	}
	if err = Migrate(ctx); err == nil {
		t.Fatal("migration unexpectedly removed a dependent view")
	}
	if _, err = tx.ExecContext(ctx, "ROLLBACK TO SAVEPOINT before_cleanup"); err != nil {
		t.Fatal(err)
	}
	for _, table := range legacy {
		var id int
		if err = tx.QueryRowContext(ctx, "SELECT id FROM "+table).Scan(&id); err != nil || id != 42 {
			t.Fatalf("failed migration changed %s: id=%d err=%v", table, id, err)
		}
	}
	if _, err = tx.ExecContext(ctx, "DROP VIEW event_consumer"); err != nil {
		t.Fatal(err)
	}
	for range 2 {
		if err = Migrate(ctx); err != nil {
			t.Fatal(err)
		}
	}
	for _, table := range legacy {
		var absent bool
		if err = tx.QueryRowContext(ctx, "SELECT to_regclass($1) IS NULL", table).Scan(&absent); err != nil || !absent {
			t.Fatalf("legacy table %s remains: absent=%v err=%v", table, absent, err)
		}
	}
	for _, table := range retained {
		var id int
		if err = tx.QueryRowContext(ctx, "SELECT id FROM "+table).Scan(&id); err != nil || id != 42 {
			t.Fatalf("migration changed retained %s: id=%d err=%v", table, id, err)
		}
	}
}
