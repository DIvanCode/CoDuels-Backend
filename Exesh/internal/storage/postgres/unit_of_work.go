package postgres

import (
	"context"
	"database/sql"
	"exesh/internal/config"
	"fmt"

	_ "github.com/jackc/pgx/v5/stdlib"
)

type UnitOfWork struct {
	db *sql.DB
}

func NewUnitOfWork(cfg config.StorageConfig) (*UnitOfWork, error) {
	db, err := sql.Open("pgx", cfg.ConnectionString)
	if err != nil {
		return nil, fmt.Errorf("failed to open database: %w", err)
	}

	return &UnitOfWork{db}, nil
}

func (u *UnitOfWork) Do(ctx context.Context, fn func(ctx context.Context) error) error {
	tx, err := u.db.BeginTx(ctx, nil)
	if err != nil {
		return fmt.Errorf("failed to begin transaction: %w", err)
	}

	if err := fn(withTx(ctx, tx)); err != nil {
		tx.Rollback()
		return err
	}

	if err = tx.Commit(); err != nil {
		return fmt.Errorf("failed to commit transaction: %w", err)
	}
	return nil
}

func withTx(ctx context.Context, tx *sql.Tx) context.Context {
	return context.WithValue(ctx, "tx", tx)
}

func extractTx(ctx context.Context) *sql.Tx {
	return ctx.Value("tx").(*sql.Tx)
}
