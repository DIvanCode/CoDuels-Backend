package testing

import "taski/internal/domain/task"

type (
	Solution struct {
		ID          SolutionID    `json:"id"`
		TaskID      task.ID       `json:"task_id"`
		ExecutionID ExecutionID   `json:"execution_id"`
		Solution    string        `json:"solution"`
		Lang        task.Language `json:"lang"`
	}

	SolutionID string
)
