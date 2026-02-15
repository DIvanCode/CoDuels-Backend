package testing

import (
	"taski/internal/domain/task"
	"taski/internal/domain/testing/execution"
	"taski/internal/domain/testing/strategy/strategies"
	"time"
)

type (
	Solution struct {
		ID                int64                      `json:"id"`
		ExternalID        ExternalSolutionID         `json:"external_id"`
		TaskID            task.ID                    `json:"task_id"`
		ExecutionID       execution.ID               `json:"execution_id"`
		Solution          string                     `json:"solution"`
		Lang              task.Language              `json:"lang"`
		TestingStrategy   strategies.TestingStrategy `json:"testing_strategy"`
		LastTestingStatus *string                    `json:"last_testing_status"`
		CreatedAt         time.Time                  `json:"created_at"`
		StartedAt         *time.Time                 `json:"started_at"`
		FinishedAt        *time.Time                 `json:"finished_at"`
	}

	ExternalSolutionID string
)

func NewSolution(
	externalID ExternalSolutionID,
	taskID task.ID,
	solution string,
	lang task.Language,
	testingStrategy strategies.TestingStrategy,
	executionID execution.ID,
) Solution {
	return Solution{
		ExternalID:      externalID,
		TaskID:          taskID,
		ExecutionID:     executionID,
		Solution:        solution,
		Lang:            lang,
		TestingStrategy: testingStrategy,
		CreatedAt:       time.Now(),
	}
}

func (sol *Solution) ProcessTime() *time.Duration {
	if sol.StartedAt == nil || sol.FinishedAt == nil {
		return nil
	}

	processTime := sol.FinishedAt.Sub(*sol.StartedAt)
	return &processTime
}
