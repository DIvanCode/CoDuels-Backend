package postgres

import (
	"context"
	"database/sql"
	"encoding/json"
	"errors"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/steps"
	"fmt"
	"log/slog"
	"time"
)

type ExecutionStorage struct {
	log *slog.Logger
}

const (
	createExecutionTableQuery = `
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

	selectExecutionForUpdateQuery = `
		SELECT id, steps, status, created_at, scheduled_at, finished_at FROM Executions
		WHERE id = $1
		FOR UPDATE
	`

	selectExecutionForScheduleQuery = `
		SELECT id, steps, status, created_at, scheduled_at, finished_at FROM Executions
		WHERE status = $1 OR (status = $2 AND scheduled_at < $3)
		ORDER BY created_at
		LIMIT 1
		FOR UPDATE;
	`

	updateExecutionQuery = `
		UPDATE Executions SET steps=$2, status=$3, created_at=$4, scheduled_at=$5, finished_at=$6
		WHERE id=$1;
	`
)

func NewExecutionStorage(ctx context.Context, log *slog.Logger) (*ExecutionStorage, error) {
	tx := extractTx(ctx)

	if _, err := tx.ExecContext(ctx, createExecutionTableQuery); err != nil {
		return nil, fmt.Errorf("failed to create execution table: %w", err)
	}

	return &ExecutionStorage{log: log}, nil
}

func (s *ExecutionStorage) CreateExecution(ctx context.Context, e execution.Execution) error {
	tx := extractTx(ctx)

	if _, err := tx.ExecContext(ctx, insertExecutionQuery,
		e.ID, e.Steps, e.Status, e.CreatedAt, e.ScheduledAt, e.FinishedAt); err != nil {
		return fmt.Errorf("failed to do insert execution query: %w", err)
	}

	return nil
}

func (s *ExecutionStorage) GetExecutionForUpdate(ctx context.Context, id execution.ID) (e *execution.Execution, err error) {
	tx := extractTx(ctx)

	e = &execution.Execution{}
	var eid string
	var stepsRaw json.RawMessage
	if err = tx.QueryRowContext(ctx, selectExecutionForUpdateQuery, id).
		Scan(&eid, &stepsRaw, &e.Status, &e.CreatedAt, &e.ScheduledAt, &e.FinishedAt); err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			e = nil
			err = nil
			return
		}
		err = fmt.Errorf("failed to do select execution for update query: %w", err)
		return
	}

	if err = e.ID.FromString(eid); err != nil {
		err = fmt.Errorf("failed to unmarshal id: %w", err)
		return
	}

	if e.Steps, err = steps.UnmarshalStepsJSON(stepsRaw); err != nil {
		err = fmt.Errorf("failed to unmarshal steps json: %w", err)
		return
	}

	return
}

func (s *ExecutionStorage) GetExecutionForSchedule(ctx context.Context, retryBefore time.Time) (e *execution.Execution, err error) {
	tx := extractTx(ctx)

	e = &execution.Execution{}
	var eid string
	var stepsRaw json.RawMessage
	if err = tx.QueryRowContext(ctx, selectExecutionForScheduleQuery,
		execution.StatusNewExecution, execution.StatusScheduledExecution, retryBefore).
		Scan(&eid, &stepsRaw, &e.Status, &e.CreatedAt, &e.ScheduledAt, &e.FinishedAt); err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			e = nil
			err = nil
			return
		}
		err = fmt.Errorf("failed to do select execution for schedule query: %w", err)
		return
	}

	if err = e.ID.FromString(eid); err != nil {
		err = fmt.Errorf("failed to unmarshal id: %w", err)
		return
	}

	if e.Steps, err = steps.UnmarshalStepsJSON(stepsRaw); err != nil {
		err = fmt.Errorf("failed to unmarshal steps json: %w", err)
		return
	}

	return
}

func (s *ExecutionStorage) SaveExecution(ctx context.Context, e execution.Execution) error {
	tx := extractTx(ctx)

	if _, err := tx.ExecContext(ctx, updateExecutionQuery,
		e.ID, e.Steps, e.Status, e.CreatedAt, e.ScheduledAt, e.FinishedAt); err != nil {
		return fmt.Errorf("failed to do update execution query: %w", err)
	}

	return nil
}
