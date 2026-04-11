package postgres

import (
	"context"
	"encoding/json"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/message/history"
	"fmt"
	"log/slog"
	"time"
)

type MessageStorage struct {
	log *slog.Logger
}

const (
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

	insertMessageQuery = `
		WITH execution_lock AS (
			SELECT id FROM Executions
			WHERE id = $1
			FOR UPDATE
		), next_id AS (
			SELECT COALESCE(MAX(message_id), 0) + 1 AS message_id
			FROM Messages
			WHERE execution_id = $1
		)
		INSERT INTO Messages(execution_id, message_id, message, created_at)
		SELECT $1, next_id.message_id, $2::jsonb, $3
		FROM execution_lock, next_id;
	`

	selectMessagesQuery = `
		SELECT message_id, message
		FROM Messages
		WHERE execution_id = $1 AND message_id >= $2
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
	executionID execution.ID,
	payload string,
	createdAt time.Time,
) error {
	tx := extractTx(ctx)

	res, err := tx.ExecContext(ctx, insertMessageQuery, executionID, payload, createdAt)
	if err != nil {
		return fmt.Errorf("failed to do insert message query: %w", err)
	}

	affected, err := res.RowsAffected()
	if err != nil {
		return fmt.Errorf("failed to fetch affected rows after insert message query: %w", err)
	}
	if affected != 1 {
		return fmt.Errorf("failed to insert message for execution %s: execution not found", executionID.String())
	}

	return nil
}

func (s *MessageStorage) GetMessages(
	ctx context.Context,
	executionID execution.ID,
	startID int64,
	count int,
) ([]history.Message, error) {
	tx := extractTx(ctx)

	rows, err := tx.QueryContext(ctx, selectMessagesQuery, executionID, startID, count)
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
