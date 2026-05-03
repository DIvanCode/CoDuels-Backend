package postgres

import (
	"context"
	"database/sql"
	"exesh/internal/scheduler"
	"fmt"
	"log/slog"
	"time"
)

type SchedulerEventStorage struct {
	log       *slog.Logger
	db        *sql.DB
	ch        chan func(context.Context) error
	retention time.Duration
}

const createSchedulerEventTablesQuery = `
	CREATE TABLE IF NOT EXISTS exesh_execution_events(
		id bigserial PRIMARY KEY,
		happened_at timestamptz NOT NULL,
		event_type text NOT NULL,
		execution_id varchar(36) NOT NULL,
		priority double precision NOT NULL DEFAULT 0,
		progress_ratio double precision NOT NULL DEFAULT 0,
		duration_seconds double precision NOT NULL DEFAULT 0,
		status text NOT NULL DEFAULT ''
	);
	CREATE INDEX IF NOT EXISTS exesh_execution_events_happened_at_idx ON exesh_execution_events(happened_at);
	CREATE INDEX IF NOT EXISTS exesh_execution_events_execution_id_idx ON exesh_execution_events(execution_id);

	CREATE TABLE IF NOT EXISTS exesh_job_events(
		id bigserial PRIMARY KEY,
		happened_at timestamptz NOT NULL,
		event_type text NOT NULL,
		job_id varchar(36) NOT NULL,
		execution_id varchar(36) NOT NULL,
		worker_id text NOT NULL DEFAULT '',
		job_type text NOT NULL DEFAULT '',
		status text NOT NULL DEFAULT '',
		expected_memory_mb integer NOT NULL DEFAULT 0,
		expected_duration_ms integer NOT NULL DEFAULT 0,
		memory_start_mb integer NOT NULL DEFAULT 0,
		memory_end_mb integer NOT NULL DEFAULT 0,
		promised_start_at timestamptz NULL,
		started_at timestamptz NULL,
		finished_at timestamptz NULL,
		expected_finished_at timestamptz NULL,
		actual_duration_seconds double precision NOT NULL DEFAULT 0,
		scheduler_latency_seconds double precision NOT NULL DEFAULT 0
	);
	CREATE INDEX IF NOT EXISTS exesh_job_events_happened_at_idx ON exesh_job_events(happened_at);
	CREATE INDEX IF NOT EXISTS exesh_job_events_job_id_idx ON exesh_job_events(job_id);
	CREATE INDEX IF NOT EXISTS exesh_job_events_execution_id_idx ON exesh_job_events(execution_id);
	CREATE INDEX IF NOT EXISTS exesh_job_events_worker_id_idx ON exesh_job_events(worker_id);

	CREATE TABLE IF NOT EXISTS exesh_worker_events(
		id bigserial PRIMARY KEY,
		happened_at timestamptz NOT NULL,
		event_type text NOT NULL,
		worker_id text NOT NULL,
		total_slots integer NOT NULL DEFAULT 0,
		total_memory_mb integer NOT NULL DEFAULT 0,
		free_slots integer NOT NULL DEFAULT 0,
		available_memory_mb integer NOT NULL DEFAULT 0,
		running_jobs integer NOT NULL DEFAULT 0,
		used_memory_mb integer NOT NULL DEFAULT 0
	);
	CREATE INDEX IF NOT EXISTS exesh_worker_events_happened_at_idx ON exesh_worker_events(happened_at);
	CREATE INDEX IF NOT EXISTS exesh_worker_events_worker_id_idx ON exesh_worker_events(worker_id);
`

func NewSchedulerEventStorage(ctx context.Context, log *slog.Logger, db *sql.DB) (*SchedulerEventStorage, error) {
	if _, err := db.ExecContext(ctx, createSchedulerEventTablesQuery); err != nil {
		return nil, fmt.Errorf("failed to create scheduler event tables: %w", err)
	}
	return &SchedulerEventStorage{
		log:       log,
		db:        db,
		ch:        make(chan func(context.Context) error, 10000),
		retention: 7 * 24 * time.Hour,
	}, nil
}

func (s *SchedulerEventStorage) Start(ctx context.Context) {
	go func() {
		for {
			select {
			case <-ctx.Done():
				return
			case write := <-s.ch:
				if err := write(ctx); err != nil {
					s.log.Error("failed to write scheduler event", slog.Any("err", err))
				}
			}
		}
	}()
	go s.runRetention(ctx)
}

func (s *SchedulerEventStorage) RecordExecutionEvent(ctx context.Context, event scheduler.ExecutionEvent) {
	s.enqueue(ctx, func(ctx context.Context) error {
		if event.At.IsZero() {
			event.At = time.Now()
		}
		_, err := s.db.ExecContext(ctx, `
			INSERT INTO exesh_execution_events(
				happened_at, event_type, execution_id, priority, progress_ratio, duration_seconds, status
			) VALUES ($1, $2, $3, $4, $5, $6, $7);
		`, event.At, event.Type, event.ExecutionID.String(), event.Priority, event.ProgressRatio, event.DurationSeconds, event.Status)
		return err
	})
}

func (s *SchedulerEventStorage) RecordJobEvent(ctx context.Context, event scheduler.JobEvent) {
	s.enqueue(ctx, func(ctx context.Context) error {
		if event.At.IsZero() {
			event.At = time.Now()
		}
		_, err := s.db.ExecContext(ctx, `
			INSERT INTO exesh_job_events(
				happened_at, event_type, job_id, execution_id, worker_id, job_type, status,
				expected_memory_mb, expected_duration_ms, memory_start_mb, memory_end_mb,
				promised_start_at, started_at, finished_at, expected_finished_at,
				actual_duration_seconds, scheduler_latency_seconds
			) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17);
		`,
			event.At, event.Type, event.JobID.String(), event.ExecutionID.String(), event.WorkerID, event.JobType, event.Status,
			event.ExpectedMemoryMB, event.ExpectedDurationMillis, event.MemoryStartMB, event.MemoryEndMB,
			nullableTime(event.PromisedStartAt), nullableTime(event.StartedAt), nullableTime(event.FinishedAt), nullableTime(event.ExpectedFinishedAt),
			event.ActualDurationSeconds, event.SchedulerLatencySeconds)
		return err
	})
}

func (s *SchedulerEventStorage) RecordWorkerEvent(ctx context.Context, event scheduler.WorkerEvent) {
	s.enqueue(ctx, func(ctx context.Context) error {
		if event.At.IsZero() {
			event.At = time.Now()
		}
		_, err := s.db.ExecContext(ctx, `
			INSERT INTO exesh_worker_events(
				happened_at, event_type, worker_id, total_slots, total_memory_mb,
				free_slots, available_memory_mb, running_jobs, used_memory_mb
			) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9);
		`, event.At, event.Type, event.WorkerID, event.TotalSlots, event.TotalMemoryMB,
			event.FreeSlots, event.AvailableMemoryMB, event.RunningJobs, event.UsedMemoryMB)
		return err
	})
}

func (s *SchedulerEventStorage) enqueue(ctx context.Context, write func(context.Context) error) {
	select {
	case s.ch <- write:
	case <-ctx.Done():
	default:
		s.log.Warn("scheduler event dropped: buffer is full")
	}
}

func (s *SchedulerEventStorage) runRetention(ctx context.Context) {
	ticker := time.NewTicker(12 * time.Hour)
	defer ticker.Stop()

	if err := s.deleteOldEvents(ctx); err != nil {
		s.log.Error("failed to delete old scheduler events", slog.Any("err", err))
	}

	for {
		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
			if err := s.deleteOldEvents(ctx); err != nil {
				s.log.Error("failed to delete old scheduler events", slog.Any("err", err))
			}
		}
	}
}

func (s *SchedulerEventStorage) deleteOldEvents(ctx context.Context) error {
	cutoff := time.Now().Add(-s.retention)
	_, err := s.db.ExecContext(ctx, `
		DELETE FROM exesh_execution_events WHERE happened_at < $1;
		DELETE FROM exesh_job_events WHERE happened_at < $1;
		DELETE FROM exesh_worker_events WHERE happened_at < $1;
	`, cutoff)
	return err
}

func nullableTime(t *time.Time) any {
	if t == nil || t.IsZero() {
		return nil
	}
	return *t
}

var _ scheduler.EventRecorder = (*SchedulerEventStorage)(nil)
