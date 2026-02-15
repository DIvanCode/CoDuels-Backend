package postgres

import (
	"context"
	"database/sql"
	"encoding/json"
	"errors"
	"fmt"
	"log/slog"
	"taski/internal/domain/testing"
	"taski/internal/domain/testing/execution"
)

type SolutionStorage struct {
	log *slog.Logger
}

const (
	createTableQuery = `
		CREATE TABLE IF NOT EXISTS Solutions(
			id bigserial PRIMARY KEY,
			external_id text,
			task_id text,
			execution_id text,
			solution text,
			lang varchar(16),
			testing_strategy jsonb,
		    last_testing_status text NULL,
		    created_at timestamp,
		    started_at timestamp NULL,
		    finished_at timestamp NULL
		);
	`

	insertQuery = `
		INSERT INTO Solutions(
		                      external_id, 
		                      task_id, 
		                      execution_id, 
		                      solution, 
		                      lang, 
		                      testing_strategy, 
		                      last_testing_status,
		                      created_at, 
		                      started_at, 
		                      finished_at)
		VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
		RETURNING id;
	`

	updateQuery = `
		UPDATE Solutions
		SET external_id=$2, 
		    task_id=$3, 
		    execution_id=$4, 
		    solution=$5, 
		    lang=$6, 
		    testing_strategy=$7, 
		    last_testing_status=$8, 
		    created_at=$9, 
		    started_at=$10, 
		    finished_at=$11
		WHERE id=$1;
	`

	selectByExecutionQuery = `
		SELECT id, 
		       external_id, 
		       task_id, 
		       execution_id, 
		       solution, 
		       lang, 
		       testing_strategy, 
		       last_testing_status, 
		       created_at, 
		       started_at, 
		       finished_at
		FROM Solutions
		WHERE execution_id=$1
		FOR UPDATE;
	`

	selectAllSolutionsQuery = `
		SELECT id, 
		       external_id, 
		       task_id, 
		       execution_id, 
		       solution, 
		       lang, 
		       testing_strategy, 
		       last_testing_status, 
		       created_at, 
		       started_at, 
		       finished_at
		FROM Solutions
	`
)

var (
	ErrSolutionByExecutionNotFound error = errors.New("solution by execution id not found")
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

	testingStrategy, err := json.Marshal(sol.TestingStrategy)
	if err != nil {
		return fmt.Errorf("failed to marshal testing strategy: %w", err)
	}

	if err := tx.QueryRowContext(ctx, insertQuery,
		sol.ExternalID,
		sol.TaskID.String(),
		sol.ExecutionID,
		sol.Solution,
		sol.Lang,
		testingStrategy,
		sol.LastTestingStatus,
		sol.CreatedAt,
		sol.StartedAt,
		sol.FinishedAt,
	).Scan(&sol.ID); err != nil {
		return fmt.Errorf("failed to do insert query: %w", err)
	}

	return nil
}

func (s *SolutionStorage) Update(ctx context.Context, sol testing.Solution) error {
	tx := extractTx(ctx)

	testingStrategy, err := json.Marshal(sol.TestingStrategy)
	if err != nil {
		return fmt.Errorf("failed to marshal testing strategy: %w", err)
	}

	if _, err := tx.ExecContext(ctx, updateQuery,
		sol.ID,
		sol.ExternalID,
		sol.TaskID.String(),
		sol.ExecutionID,
		sol.Solution,
		sol.Lang,
		testingStrategy,
		sol.LastTestingStatus,
		sol.CreatedAt,
		sol.StartedAt,
		sol.FinishedAt,
	); err != nil {
		return fmt.Errorf("failed to do update query: %w", err)
	}

	return nil
}

func (s *SolutionStorage) GetByExecutionID(ctx context.Context, executionID execution.ID) (sol testing.Solution, err error) {
	tx := extractTx(ctx)

	sol = testing.Solution{}
	var taskID string
	var testingStrategy json.RawMessage
	if err = tx.QueryRowContext(ctx, selectByExecutionQuery, executionID).Scan(
		&sol.ID,
		&sol.ExternalID,
		&taskID,
		&sol.ExecutionID,
		&sol.Solution,
		&sol.Lang,
		&testingStrategy,
		&sol.LastTestingStatus,
		&sol.CreatedAt,
		&sol.StartedAt,
		&sol.FinishedAt,
	); err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			err = ErrSolutionByExecutionNotFound
			return
		}
		err = fmt.Errorf("failed to do select query: %w", err)
		return
	}

	if err = sol.TaskID.FromString(taskID); err != nil {
		err = fmt.Errorf("failed to unmarshal task id: %w", err)
		return
	}
	if err = json.Unmarshal(testingStrategy, &sol.TestingStrategy); err != nil {
		err = fmt.Errorf("failed to unmarshal testing strategy: %w", err)
		return
	}

	return
}

func (s *SolutionStorage) GetAll(ctx context.Context) (solutions []testing.Solution, err error) {
	tx := extractTx(ctx)

	var rows *sql.Rows
	rows, err = tx.QueryContext(ctx, selectAllSolutionsQuery)
	if err != nil {
		err = fmt.Errorf("failed to do select query: %w", err)
		return
	}
	defer func() { _ = rows.Close() }()

	for rows.Next() {
		var sol testing.Solution
		var taskID string
		var testingStrategy json.RawMessage
		if err = rows.Scan(
			&sol.ID,
			&sol.ExternalID,
			&taskID,
			&sol.ExecutionID,
			&sol.Solution,
			&sol.Lang,
			&testingStrategy,
			&sol.LastTestingStatus,
			&sol.CreatedAt,
			&sol.StartedAt,
			&sol.FinishedAt,
		); err != nil {
			err = fmt.Errorf("failed to do select query: %w", err)
			return
		}
		if err = sol.TaskID.FromString(taskID); err != nil {
			err = fmt.Errorf("failed to unmarshal task id: %w", err)
			return
		}
		if err = json.Unmarshal(testingStrategy, &sol.TestingStrategy); err != nil {
			err = fmt.Errorf("failed to unmarshal testing strategy: %w", err)
			return
		}
		solutions = append(solutions, sol)
	}

	return
}
