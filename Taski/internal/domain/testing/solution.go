package testing

import (
	"taski/internal/domain/task"
	"time"
)

type (
	Solution struct {
		ID          SolutionID     `json:"id"`
		TaskID      task.ID        `json:"task_id"`
		ExecutionID ExecutionID    `json:"execution_id"`
		Solution    string         `json:"solution"`
		Lang        task.Language  `json:"lang"`
		Tests       int            `json:"tests"`
		Status      map[int]string `json:"status"`
		CreatedAt   time.Time      `json:"created_at"`
		FinishedAt  *time.Time     `json:"finished_at"`
	}

	SolutionID string
)

func (s Solution) GetTestedPrefix() (id int) {
	for id = 0; id+1 <= s.Tests && s.Status[id+1] != "?"; id++ {
	}
	return
}

func (s Solution) AllTestsPassed() bool {
	for id := 1; id <= s.Tests; id++ {
		if s.Status[id] != "+" {
			return false
		}
	}
	return true
}

func (s Solution) ProcessTime() time.Duration {
	return s.FinishedAt.Sub(s.CreatedAt)
}
