package postgres

import (
	"context"
	"fmt"
)

const (
	createExecutionTableQuery = `
		CREATE TABLE IF NOT EXISTS Executions(
			id varchar(36) PRIMARY KEY,
			stages jsonb,
		    sources jsonb,
			weight bigint NOT NULL DEFAULT 0,
			tries integer NOT NULL DEFAULT 0,
			status varchar(32),
			created_at timestamp,
			scheduled_at timestamp NULL,
			finished_at timestamp NULL
		);
	`

	addWeightToExecutionTableQuery = `
		ALTER TABLE Executions
		ADD COLUMN IF NOT EXISTS weight bigint NOT NULL DEFAULT 0;
	`

	addTriesToExecutionTableQuery = `
		ALTER TABLE Executions
		ADD COLUMN IF NOT EXISTS tries integer NOT NULL DEFAULT 0;
	`

	createOutboxTableQuery = `
		CREATE TABLE IF NOT EXISTS Outbox(
		    id BIGSERIAL PRIMARY KEY,
			message text,
			created_at timestamp,
			failed_at timestamp NULL,
			failed_tries integer
		);
	`

	createMessageTableQuery = `
		CREATE TABLE IF NOT EXISTS Messages(
			execution_id varchar(36) NOT NULL,
			message_id bigint NOT NULL,
			message jsonb NOT NULL,
			created_at timestamp NOT NULL,
			PRIMARY KEY (execution_id, message_id),
			FOREIGN KEY (execution_id) REFERENCES Executions(id) ON DELETE CASCADE
		);
	`

	createCategoryTimeHistogramTableQuery = `
		CREATE TABLE IF NOT EXISTS category_time_histogram(
			category_name text NOT NULL,
			time_bucket_ms integer NOT NULL,
			cnt bigint NOT NULL DEFAULT 0,
			PRIMARY KEY (category_name, time_bucket_ms),
			CONSTRAINT category_time_histogram_bucket_non_negative CHECK (time_bucket_ms >= 0),
			CONSTRAINT category_time_histogram_cnt_non_negative CHECK (cnt >= 0)
		);
	`

	createCategoryMemoryHistogramTableQuery = `
		CREATE TABLE IF NOT EXISTS category_memory_histogram(
			category_name text NOT NULL,
			memory_bucket_mb integer NOT NULL,
			cnt bigint NOT NULL DEFAULT 0,
			PRIMARY KEY (category_name, memory_bucket_mb),
			CONSTRAINT category_memory_histogram_bucket_non_negative CHECK (memory_bucket_mb >= 0),
			CONSTRAINT category_memory_histogram_cnt_non_negative CHECK (cnt >= 0)
		);
	`

	createCategoryTimeHistogramCategoryIdxQuery = `
		CREATE INDEX IF NOT EXISTS idx_category_time_histogram_category
		ON category_time_histogram(category_name);
	`

	createCategoryMemoryHistogramCategoryIdxQuery = `
		CREATE INDEX IF NOT EXISTS idx_category_memory_histogram_category
		ON category_memory_histogram(category_name);
	`
)

// Migrate applies the complete Exesh schema in the caller's startup transaction.
func Migrate(ctx context.Context) error {
	steps := []struct {
		name  string
		query string
	}{
		// Drop only the retired dashboard tables. The category histograms are
		// required for scheduling estimates and retain their existing rows.
		{"legacy dashboard events", `DROP TABLE IF EXISTS exesh_execution_events, exesh_job_events, exesh_worker_events;`},
		{"createExecutionTableQuery", createExecutionTableQuery},
		{"addWeightToExecutionTableQuery", addWeightToExecutionTableQuery},
		{"addTriesToExecutionTableQuery", addTriesToExecutionTableQuery},
		{"createOutboxTableQuery", createOutboxTableQuery},
		{"createMessageTableQuery", createMessageTableQuery},
		{"createCategoryTimeHistogramTableQuery", createCategoryTimeHistogramTableQuery},
		{"createCategoryMemoryHistogramTableQuery", createCategoryMemoryHistogramTableQuery},
		{"createCategoryTimeHistogramCategoryIdxQuery", createCategoryTimeHistogramCategoryIdxQuery},
		{"createCategoryMemoryHistogramCategoryIdxQuery", createCategoryMemoryHistogramCategoryIdxQuery},
	}
	tx := extractTx(ctx)
	for _, step := range steps {
		if _, err := tx.ExecContext(ctx, step.query); err != nil {
			return fmt.Errorf("failed to migrate %s: %w", step.name, err)
		}
	}
	return nil
}
