package postgres

import (
	"context"
	"database/sql"
	"errors"
	"exesh/internal/domain/outbox"
	"fmt"
	"log/slog"
)

type OutboxStorage struct {
	log *slog.Logger
}

const (
	insertOutboxQuery = `
		INSERT INTO Outbox(message, created_at, failed_at, failed_tries)
		VALUES ($1, $2, $3, $4);
	`

	selectOutboxForSendQuery = `
		SELECT id, message, created_at, failed_at, failed_tries FROM Outbox
		ORDER BY created_at
		LIMIT 1
		FOR UPDATE
	`

	updateOutboxQuery = `
		UPDATE Outbox SET message=$2, created_at=$3, failed_at=$4, failed_tries=$5
		WHERE id=$1;
	`

	deleteOutboxQuery = `
		DELETE FROM Outbox
		WHERE id=$1;
	`
)

func NewOutboxStorage(log *slog.Logger) *OutboxStorage {
	return &OutboxStorage{log: log}
}

func (s *OutboxStorage) CreateOutbox(ctx context.Context, ox outbox.Outbox) error {
	tx := extractTx(ctx)

	if _, err := tx.ExecContext(ctx, insertOutboxQuery,
		ox.Payload, ox.CreatedAt, ox.FailedAt, ox.FailedTries); err != nil {
		return fmt.Errorf("failed to do insert outbox query: %w", err)
	}

	return nil
}

func (s *OutboxStorage) GetOutboxForSend(ctx context.Context) (ox *outbox.Outbox, err error) {
	tx := extractTx(ctx)

	ox = &outbox.Outbox{}
	if err = tx.QueryRowContext(ctx, selectOutboxForSendQuery).
		Scan(&ox.ID, &ox.Payload, &ox.CreatedAt, &ox.FailedAt, &ox.FailedTries); err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			ox = nil
			err = nil
			return
		}
		err = fmt.Errorf("failed to do select outbox for send query: %w", err)
		return
	}

	return
}

func (s *OutboxStorage) SaveOutbox(ctx context.Context, ox outbox.Outbox) error {
	tx := extractTx(ctx)

	if _, err := tx.ExecContext(ctx, updateOutboxQuery,
		ox.ID, ox.Payload, ox.CreatedAt, ox.FailedAt, ox.FailedTries); err != nil {
		return fmt.Errorf("failed to do update outbox query: %w", err)
	}

	return nil
}

func (s *OutboxStorage) DeleteOutbox(ctx context.Context, ox outbox.Outbox) error {
	tx := extractTx(ctx)

	if _, err := tx.ExecContext(ctx, deleteOutboxQuery, ox.ID); err != nil {
		return fmt.Errorf("failed to do delete outbox query: %w", err)
	}

	return nil
}
