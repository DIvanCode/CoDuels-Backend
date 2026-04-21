package calculator

import (
	"context"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/job/jobs"
)

type CategoryStatsStorage interface {
	GetExpectedByCategories(context.Context, []string) (execution.CategoryStats, error)
}

type Calculator struct {
	statsStorage CategoryStatsStorage
}

func NewCalculator(statsStorage CategoryStatsStorage) *Calculator {
	return &Calculator{statsStorage: statsStorage}
}

func (c *Calculator) LoadCategoryStats(
	ctx context.Context,
	stageDefs execution.StageDefinitions,
) (execution.CategoryStats, error) {
	stats := execution.NewCategoryStats()

	categories := make([]string, 0)
	seen := make(map[string]struct{})
	for _, stageDef := range stageDefs {
		for _, jobDef := range stageDef.Jobs {
			category := jobDef.GetCategoryName()
			if category == "" {
				continue
			}
			if _, ok := seen[category]; ok {
				continue
			}
			seen[category] = struct{}{}
			categories = append(categories, category)
		}
	}

	if len(categories) == 0 {
		return stats, nil
	}

	return c.statsStorage.GetExpectedByCategories(ctx, categories)
}

func (c *Calculator) EstimateForJob(
	jobDef jobs.Definition,
	stats execution.CategoryStats,
) (expectedTime int, expectedMemory int) {
	categoryName := jobDef.GetCategoryName()
	timeLimit := jobDef.GetTimeLimit()
	memoryLimit := jobDef.GetMemoryLimit()

	expectedTime = estimateExpectedTime(
		stats.TimeSamplesByCategory[categoryName],
		stats.MedianTimeByCategory[categoryName],
		stats.MaxTimeByCategory[categoryName],
		timeLimit,
	)
	expectedMemory = estimateExpectedMemory(
		stats.MemorySamplesByCategory[categoryName],
		stats.MedianMemoryByCategory[categoryName],
		stats.MaxMemoryByCategory[categoryName],
		memoryLimit,
	)
	return expectedTime, expectedMemory
}

func (c *Calculator) CalculateWeight(
	stageDefs execution.StageDefinitions,
	stats execution.CategoryStats,
) int64 {
	var weight int64
	for _, stageDef := range stageDefs {
		for _, jobDef := range stageDef.Jobs {
			expectedTime, expectedMemory := c.EstimateForJob(jobDef, stats)
			weight += int64(expectedTime) * int64(expectedMemory)
		}
	}
	return weight
}

func estimateExpectedTime(timeSamples int, median int, max int, timeLimit int) int {
	if timeSamples == 0 {
		return timeLimit
	}
	value := (3*max + 7*median) / 10
	return clamp(value, 100, timeLimit)
}

func estimateExpectedMemory(memorySamples int, median int, max int, memoryLimit int) int {
	if memorySamples == 0 {
		return memoryLimit
	}
	value := (3*max + 7*median) / 10
	return clamp(value, 16, memoryLimit)
}

func clamp(value int, minValue int, maxValue int) int {
	if maxValue < minValue {
		maxValue = minValue
	}
	if value < minValue {
		return minValue
	}
	if value > maxValue {
		return maxValue
	}
	return value
}
