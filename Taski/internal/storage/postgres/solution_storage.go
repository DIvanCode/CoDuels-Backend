package postgres

import (
	"context"
	"database/sql"
	"encoding/json"
	"errors"
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
			language varchar(16),
			tests integer,
			status jsonb
		);
	`

	insertQuery = `
		INSERT INTO Solutions(id, task_id, execution_id, solution, language, tests, status)
		VALUES ($1, $2, $3, $4, $5, $6, $7);
	`

	updateQuery = `
		UPDATE Solutions
		SET task_id=$2, execution_id=$3, solution=$4, language=$5, tests=$6, status=$7
		WHERE id=$1;
	`

	selectByExecutionQuery = `
		SELECT id, task_id, execution_id, solution, language, tests, status
		FROM Solutions
		WHERE execution_id=$1
		FOR UPDATE;
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
		sol.ID, sol.TaskID.String(), sol.ExecutionID, sol.Solution, sol.Lang, sol.Tests, sol.Status); err != nil {
		return fmt.Errorf("failed to do insert query: %w", err)
	}

	return nil
}

func (s *SolutionStorage) Update(ctx context.Context, sol testing.Solution) error {
	tx := extractTx(ctx)

	if _, err := tx.ExecContext(ctx, updateQuery,
		sol.ID, sol.TaskID.String(), sol.ExecutionID, sol.Solution, sol.Lang, sol.Tests, sol.Status); err != nil {
		return fmt.Errorf("failed to do update query: %w", err)
	}

	return nil
}

func (s *SolutionStorage) GetByExecutionID(ctx context.Context, executionID testing.ExecutionID) (sol testing.Solution, err error) {
	tx := extractTx(ctx)

	sol = testing.Solution{}
	var taskID string
	var status json.RawMessage
	if err = tx.QueryRowContext(ctx, selectByExecutionQuery, executionID).
		Scan(&sol.ID, &taskID, &sol.ExecutionID, &sol.Solution, &sol.Lang, &sol.Tests, &status); err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			err = fmt.Errorf("solution with execution id %s not found", executionID)
			return
		}
		err = fmt.Errorf("failed to do select query: %w", err)
		return
	}

	if err = sol.TaskID.FromString(taskID); err != nil {
		err = fmt.Errorf("failed to unmarshal task id: %w", err)
		return
	}
	if err = json.Unmarshal(status, &sol.Status); err != nil {
		err = fmt.Errorf("failed to unmarshal status: %w", err)
		return
	}

	return
}
