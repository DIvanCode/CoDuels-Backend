package postgres

import (
	"context"
	"exesh/internal/domain/execution"
	"fmt"
	"log/slog"
)

type CategoryHistogramStorage struct {
	log *slog.Logger
}

const (
	createCategoryTimeHistogramTableQuery = `
		CREATE TABLE IF NOT EXISTS category_time_histogram(
			category_name text NOT NULL,
			time_bucket_ms integer NOT NULL,
			cnt bigint NOT NULL DEFAULT 0,
			PRIMARY KEY (category_name, time_bucket_ms),
			CONSTRAINT category_time_histogram_bucket_non_negative CHECK (time_bucket_ms >= 0),
			CONSTRAINT category_time_histogram_cnt_non_negative CHECK (cnt >= 0)
		);
	`

	createCategoryMemoryHistogramTableQuery = `
		CREATE TABLE IF NOT EXISTS category_memory_histogram(
			category_name text NOT NULL,
			memory_bucket_mb integer NOT NULL,
			cnt bigint NOT NULL DEFAULT 0,
			PRIMARY KEY (category_name, memory_bucket_mb),
			CONSTRAINT category_memory_histogram_bucket_non_negative CHECK (memory_bucket_mb >= 0),
			CONSTRAINT category_memory_histogram_cnt_non_negative CHECK (cnt >= 0)
		);
	`

	createCategoryTimeHistogramCategoryIdxQuery = `
		CREATE INDEX IF NOT EXISTS idx_category_time_histogram_category
		ON category_time_histogram(category_name);
	`

	createCategoryMemoryHistogramCategoryIdxQuery = `
		CREATE INDEX IF NOT EXISTS idx_category_memory_histogram_category
		ON category_memory_histogram(category_name);
	`

	upsertCategoryTimeHistogramQuery = `
		INSERT INTO category_time_histogram(category_name, time_bucket_ms, cnt)
		VALUES ($1, $2, 1)
		ON CONFLICT (category_name, time_bucket_ms)
		DO UPDATE SET cnt = category_time_histogram.cnt + 1;
	`

	upsertCategoryMemoryHistogramQuery = `
		INSERT INTO category_memory_histogram(category_name, memory_bucket_mb, cnt)
		VALUES ($1, $2, 1)
		ON CONFLICT (category_name, memory_bucket_mb)
		DO UPDATE SET cnt = category_memory_histogram.cnt + 1;
	`

	selectCategoryExpectedQuery = `
		WITH categories AS (
			SELECT DISTINCT unnest($1::text[]) AS category_name
		),
		time_rows AS (
			SELECT
				c.category_name,
				h.time_bucket_ms,
				h.cnt,
				SUM(h.cnt) OVER (PARTITION BY c.category_name ORDER BY h.time_bucket_ms) AS cumulative_cnt,
				SUM(h.cnt) OVER (PARTITION BY c.category_name) AS total_cnt
			FROM categories c
			LEFT JOIN category_time_histogram h ON h.category_name = c.category_name
		),
		time_stats AS (
			SELECT
				category_name,
				MAX(total_cnt) AS time_samples,
				MAX(time_bucket_ms) + 50 AS max_time_ms,
				MIN(CASE WHEN cumulative_cnt >= CEIL(total_cnt::numeric * 0.5) THEN (time_bucket_ms + 50) END) AS median_time_ms
			FROM time_rows
			WHERE total_cnt > 0
			GROUP BY category_name
		),
		memory_rows AS (
			SELECT
				c.category_name,
				h.memory_bucket_mb,
				h.cnt,
				SUM(h.cnt) OVER (PARTITION BY c.category_name ORDER BY h.memory_bucket_mb) AS cumulative_cnt,
				SUM(h.cnt) OVER (PARTITION BY c.category_name) AS total_cnt
			FROM categories c
			LEFT JOIN category_memory_histogram h ON h.category_name = c.category_name
		),
		memory_stats AS (
			SELECT
				category_name,
				MAX(total_cnt) AS memory_samples,
				MAX(memory_bucket_mb) + 16 AS max_memory_mb,
				MIN(CASE WHEN cumulative_cnt >= CEIL(total_cnt::numeric * 0.5) THEN (memory_bucket_mb + 16) END) AS median_memory_mb
			FROM memory_rows
			WHERE total_cnt > 0
			GROUP BY category_name
		)
		SELECT
			c.category_name,
			COALESCE(t.time_samples, 0) AS time_samples,
			COALESCE(t.median_time_ms, 0) AS median_time_ms,
			COALESCE(t.max_time_ms, 0) AS max_time_ms,
			COALESCE(m.memory_samples, 0) AS memory_samples,
			COALESCE(m.median_memory_mb, 0) AS median_memory_mb,
			COALESCE(m.max_memory_mb, 0) AS max_memory_mb
		FROM categories c
		LEFT JOIN time_stats t ON t.category_name = c.category_name
		LEFT JOIN memory_stats m ON m.category_name = c.category_name;
	`
)

func NewCategoryHistogramStorage(ctx context.Context, log *slog.Logger) (*CategoryHistogramStorage, error) {
	tx := extractTx(ctx)

	if _, err := tx.ExecContext(ctx, createCategoryTimeHistogramTableQuery); err != nil {
		return nil, fmt.Errorf("failed to create category_time_histogram table: %w", err)
	}
	if _, err := tx.ExecContext(ctx, createCategoryMemoryHistogramTableQuery); err != nil {
		return nil, fmt.Errorf("failed to create category_memory_histogram table: %w", err)
	}
	if _, err := tx.ExecContext(ctx, createCategoryTimeHistogramCategoryIdxQuery); err != nil {
		return nil, fmt.Errorf("failed to create category_time_histogram index: %w", err)
	}
	if _, err := tx.ExecContext(ctx, createCategoryMemoryHistogramCategoryIdxQuery); err != nil {
		return nil, fmt.Errorf("failed to create category_memory_histogram index: %w", err)
	}

	return &CategoryHistogramStorage{log: log}, nil
}

func (s *CategoryHistogramStorage) UpdateCategoryHistogram(
	ctx context.Context,
	categoryName string,
	elapsedTimeMs int,
	usedMemoryMb int,
) error {
	tx := extractTx(ctx)

	timeBucketMs := bucketTimeMs(elapsedTimeMs)
	memoryBucketMb := bucketMemoryMb(usedMemoryMb)

	if _, err := tx.ExecContext(ctx, upsertCategoryTimeHistogramQuery, categoryName, timeBucketMs); err != nil {
		return fmt.Errorf("failed to upsert category_time_histogram: %w", err)
	}
	if _, err := tx.ExecContext(ctx, upsertCategoryMemoryHistogramQuery, categoryName, memoryBucketMb); err != nil {
		return fmt.Errorf("failed to upsert category_memory_histogram: %w", err)
	}

	return nil
}

func (s *CategoryHistogramStorage) GetExpectedByCategories(
	ctx context.Context,
	categoryNames []string,
) (execution.CategoryStats, error) {
	stats := execution.NewCategoryStats()
	if len(categoryNames) == 0 {
		return stats, nil
	}

	tx := extractTx(ctx)

	rows, err := tx.QueryContext(ctx, selectCategoryExpectedQuery, categoryNames)
	if err != nil {
		return execution.CategoryStats{}, fmt.Errorf("failed to query expected values by categories: %w", err)
	}
	defer rows.Close()

	for rows.Next() {
		var categoryName string
		var timeSamples int
		var medianTimeMs int
		var maxTimeMs int
		var memorySamples int
		var medianMemoryMb int
		var maxMemoryMb int
		if err = rows.Scan(
			&categoryName,
			&timeSamples,
			&medianTimeMs,
			&maxTimeMs,
			&memorySamples,
			&medianMemoryMb,
			&maxMemoryMb,
		); err != nil {
			return execution.CategoryStats{}, fmt.Errorf("failed to scan expected values by categories row: %w", err)
		}
		stats.TimeSamplesByCategory[categoryName] = timeSamples
		stats.MedianTimeByCategory[categoryName] = medianTimeMs
		stats.MaxTimeByCategory[categoryName] = maxTimeMs
		stats.MemorySamplesByCategory[categoryName] = memorySamples
		stats.MedianMemoryByCategory[categoryName] = medianMemoryMb
		stats.MaxMemoryByCategory[categoryName] = maxMemoryMb
	}

	if err = rows.Err(); err != nil {
		return execution.CategoryStats{}, fmt.Errorf("failed while iterate expected values by categories rows: %w", err)
	}

	return stats, nil
}

func bucketTimeMs(elapsedTimeMs int) int {
	if elapsedTimeMs < 0 {
		elapsedTimeMs = 0
	}
	return (elapsedTimeMs / 50) * 50
}

func bucketMemoryMb(usedMemoryMb int) int {
	if usedMemoryMb < 0 {
		usedMemoryMb = 0
	}
	return (usedMemoryMb / 16) * 16
}
