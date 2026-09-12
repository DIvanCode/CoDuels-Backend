package postgres

import (
	"context"
	"fmt"
)

// Migrate runs idempotent schema changes in the storage initialization transaction.
func Migrate(ctx context.Context) error {
	// These tables only backed the retired dashboard. Category histograms are
	// still used for scheduling estimates and must retain their samples.
	_, err := extractTx(ctx).ExecContext(ctx, `
		DROP TABLE IF EXISTS exesh_execution_events, exesh_job_events, exesh_worker_events;
	`)
	if err != nil {
		return fmt.Errorf("failed to drop legacy scheduler event tables: %w", err)
	}
	return nil
}
