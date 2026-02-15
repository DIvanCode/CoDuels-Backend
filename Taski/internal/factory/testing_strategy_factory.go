package factory

import (
	"fmt"
	"github.com/DIvanCode/filestorage/pkg/bucket"
	"taski/internal/domain/task"
	"taski/internal/domain/testing/source/sources"
	"taski/internal/domain/testing/strategy"
	"taski/internal/domain/testing/strategy/strategies"
)

type TestingStrategyFactory struct{}

func NewTestingStrategyFactory() *TestingStrategyFactory {
	return &TestingStrategyFactory{}
}

func (f *TestingStrategyFactory) CreateStrategy(
	t task.Task,
	solution string,
	lang task.Language,
	downloadEndpoint string,
) (strategies.TestingStrategy, error) {
	var taskBucket bucket.ID
	if err := taskBucket.FromString(t.GetID().String()); err != nil {
		return strategies.TestingStrategy{}, fmt.Errorf("failed to convert task id to bucket id: %w", err)
	}

	taskSource := sources.NewFilestorageBucketSource(strategy.TaskSource, taskBucket, downloadEndpoint)

	switch t.GetType() {
	case task.WriteCode:
		return strategies.NewWriteCodeTaskTestingStrategy(t, taskSource, solution, lang)
	case task.FindTest:
		return strategies.NewFindTestTaskTestingStrategy(t, taskSource, solution)
	case task.PredictOutput:
		return strategies.NewPredictOutputTaskTestingStrategy(t, taskSource, solution)
	default:
		return strategies.TestingStrategy{}, fmt.Errorf("unsupported task type %s", t.GetType())
	}
}
