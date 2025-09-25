package postgres

import (
	"context"
	"exesh/internal/domain/execution"
	"fmt"
	"log/slog"
)

type ExecutionStorage struct {
	log *slog.Logger
}

const (
	createTableQuery = `
		CREATE TABLE IF NOT EXISTS Executions(
			id varchar(36) PRIMARY KEY,
			steps jsonb,
			status varchar(32),
			created_at timestamp,
			scheduled_at timestamp NULL,
			finished_at timestamp NULL
		);
	`

	insertExecutionQuery = `
		INSERT INTO Executions(id, steps, status, created_at, scheduled_at, finished_at)
		VALUES ($1, $2, $3, $4, $5, $6);
	`
)

func NewExecutionStorage(ctx context.Context, log *slog.Logger) (*ExecutionStorage, error) {
	tx := extractTx(ctx)
	if _, err := tx.ExecContext(ctx, createTableQuery); err != nil {
		return nil, fmt.Errorf("failed to create table: %w", err)
	}

	return &ExecutionStorage{log: log}, nil
}

func (s *ExecutionStorage) Create(ctx context.Context, e execution.Execution) error {
	tx := extractTx(ctx)
	_, err := tx.ExecContext(ctx, insertExecutionQuery,
		e.ID, e.Steps, e.Status, e.CreatedAt, e.ScheduledAt, e.FinishedAt)
	return err
}
