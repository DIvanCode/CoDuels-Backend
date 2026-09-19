package postgres

import (
	"context"
	"database/sql"
	"errors"
	"exesh/internal/domain/execution"
	"fmt"
	"log/slog"
	"time"
)

type ExecutionStorage struct {
	log *slog.Logger
}

const (
	insertExecutionQuery = `
		INSERT INTO Executions(id, stages, sources, weight, tries, status, created_at, scheduled_at, finished_at)
		VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9);
	`

	selectExecutionForUpdateQuery = `
		SELECT id, stages, sources, weight, tries, status, created_at, scheduled_at, finished_at FROM Executions
		WHERE id = $1
		FOR UPDATE
	`

	selectExecutionForScheduleQuery = `
		SELECT id, stages, sources, weight, tries, status, created_at, scheduled_at, finished_at FROM Executions
		WHERE status = $1 OR (status = $2 AND scheduled_at < $3)
		ORDER BY created_at
		LIMIT 1
		FOR UPDATE SKIP LOCKED;
	`

	updateExecutionQuery = `
		UPDATE Executions SET stages=$2, sources=$3, weight=$4, tries=$5, status=$6, created_at=$7, scheduled_at=$8, finished_at=$9
		WHERE id=$1;
	`
)

func NewExecutionStorage(log *slog.Logger) *ExecutionStorage {
	return &ExecutionStorage{log: log}
}

func (s *ExecutionStorage) CreateExecution(ctx context.Context, ex execution.Definition) error {
	tx := extractTx(ctx)

	if _, err := tx.ExecContext(ctx, insertExecutionQuery,
		ex.ID, ex.Stages, ex.Sources, ex.Weight, ex.Tries, ex.Status, ex.CreatedAt, ex.ScheduledAt, ex.FinishedAt); err != nil {
		return fmt.Errorf("failed to do insert execution query: %w", err)
	}

	return nil
}

func (s *ExecutionStorage) GetExecutionForUpdate(ctx context.Context, id execution.ID) (*execution.Definition, error) {
	tx := extractTx(ctx)

	ex := execution.Definition{}
	if err := tx.QueryRowContext(ctx, selectExecutionForUpdateQuery, id).
		Scan(&ex.ID, &ex.Stages, &ex.Sources, &ex.Weight, &ex.Tries, &ex.Status, &ex.CreatedAt, &ex.ScheduledAt, &ex.FinishedAt); err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, nil
		}
		return nil, fmt.Errorf("failed to do select execution for update query: %w", err)
	}

	return &ex, nil
}

func (s *ExecutionStorage) GetExecutionForSchedule(
	ctx context.Context,
	retryBefore time.Time,
) (*execution.Definition, error) {
	tx := extractTx(ctx)

	ex := execution.Definition{}
	if err := tx.QueryRowContext(ctx, selectExecutionForScheduleQuery,
		execution.StatusNew, execution.StatusScheduled, retryBefore).
		Scan(&ex.ID, &ex.Stages, &ex.Sources, &ex.Weight, &ex.Tries, &ex.Status, &ex.CreatedAt, &ex.ScheduledAt, &ex.FinishedAt); err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return nil, nil
		}
		return nil, fmt.Errorf("failed to do select execution for schedule query: %w", err)
	}

	return &ex, nil
}

func (s *ExecutionStorage) SaveExecution(ctx context.Context, ex execution.Definition) error {
	tx := extractTx(ctx)

	if _, err := tx.ExecContext(ctx, updateExecutionQuery,
		ex.ID, ex.Stages, ex.Sources, ex.Weight, ex.Tries, ex.Status, ex.CreatedAt, ex.ScheduledAt, ex.FinishedAt); err != nil {
		return fmt.Errorf("failed to do update execution query: %w", err)
	}

	return nil
}
