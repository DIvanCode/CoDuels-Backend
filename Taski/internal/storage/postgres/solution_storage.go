package postgres

import (
	"context"
	"fmt"
	"log/slog"
	"taski/internal/domain/testing"
)

type SolutionStorage struct {
	log *slog.Logger
}

const (
	createTableQuery = `
		CREATE TABLE IF NOT EXISTS Solutions(
			id text PRIMARY KEY,
			task_id text,
			execution_id text,
			solution text,
			language varchar(16)
		);
	`

	insertQuery = `
		INSERT INTO Solutions(id, task_id, execution_id, solution, language)
		VALUES ($1, $2, $3, $4, $5);
	`
)

func NewSolutionStorage(ctx context.Context, log *slog.Logger) (*SolutionStorage, error) {
	tx := extractTx(ctx)

	if _, err := tx.ExecContext(ctx, createTableQuery); err != nil {
		return nil, fmt.Errorf("failed to create table: %w", err)
	}

	return &SolutionStorage{log: log}, nil
}

func (s *SolutionStorage) Create(ctx context.Context, sol testing.Solution) error {
	tx := extractTx(ctx)

	if _, err := tx.ExecContext(ctx, insertQuery,
		sol.ID, sol.TaskID.String(), sol.ExecutionID, sol.Solution, sol.Lang); err != nil {
		return fmt.Errorf("failed to do insert query: %w", err)
	}

	return nil
}
