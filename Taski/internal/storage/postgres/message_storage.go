package postgres

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"taski/internal/domain/testing"
	"taski/internal/domain/testing/message/history"
	"time"
)

type MessageStorage struct {
	log *slog.Logger
}

const (
	createMessageTableQuery = `
		CREATE TABLE IF NOT EXISTS Messages(
			solution_id text NOT NULL,
			message_id bigint NOT NULL,
			message jsonb NOT NULL,
			created_at timestamp NOT NULL,
			PRIMARY KEY (solution_id, message_id)
		);
	`

	insertMessageQuery = `
		WITH solution_lock AS (
			SELECT external_id
			FROM Solutions
			WHERE external_id = $1
			FOR UPDATE
		), next_id AS (
			SELECT COALESCE(MAX(message_id), 0) + 1 AS message_id
			FROM Messages
			WHERE solution_id = $1
		)
		INSERT INTO Messages(solution_id, message_id, message, created_at)
		SELECT $1, next_id.message_id, $2::jsonb, $3
		FROM solution_lock, next_id;
	`

	selectMessagesQuery = `
		SELECT message_id, message
		FROM Messages
		WHERE solution_id = $1 AND message_id >= $2
		ORDER BY message_id
		LIMIT $3;
	`
)

func NewMessageStorage(ctx context.Context, log *slog.Logger) (*MessageStorage, error) {
	tx := extractTx(ctx)

	if _, err := tx.ExecContext(ctx, createMessageTableQuery); err != nil {
		return nil, fmt.Errorf("failed to create messages table: %w", err)
	}

	return &MessageStorage{log: log}, nil
}

func (s *MessageStorage) CreateMessage(
	ctx context.Context,
	solutionID testing.ExternalSolutionID,
	payload string,
	createdAt time.Time,
) error {
	tx := extractTx(ctx)

	res, err := tx.ExecContext(ctx, insertMessageQuery, solutionID, payload, createdAt)
	if err != nil {
		return fmt.Errorf("failed to do insert message query: %w", err)
	}

	affected, err := res.RowsAffected()
	if err != nil {
		return fmt.Errorf("failed to fetch affected rows after insert message query: %w", err)
	}
	if affected != 1 {
		return fmt.Errorf("failed to insert message for solution %s: solution not found", solutionID)
	}

	return nil
}

func (s *MessageStorage) GetMessages(
	ctx context.Context,
	solutionID testing.ExternalSolutionID,
	startID int64,
	count int,
) ([]history.Message, error) {
	tx := extractTx(ctx)

	rows, err := tx.QueryContext(ctx, selectMessagesQuery, solutionID, startID, count)
	if err != nil {
		return nil, fmt.Errorf("failed to do select messages query: %w", err)
	}
	defer rows.Close()

	res := make([]history.Message, 0, count)
	for rows.Next() {
		msg := history.Message{}
		var payload []byte
		if err = rows.Scan(&msg.MessageID, &payload); err != nil {
			return nil, fmt.Errorf("failed to scan messages row: %w", err)
		}

		if err = json.Unmarshal(payload, &msg.Message); err != nil {
			return nil, fmt.Errorf("failed to unmarshal message payload: %w", err)
		}

		res = append(res, msg)
	}

	if err = rows.Err(); err != nil {
		return nil, fmt.Errorf("failed while iterate messages rows: %w", err)
	}

	return res, nil
}
