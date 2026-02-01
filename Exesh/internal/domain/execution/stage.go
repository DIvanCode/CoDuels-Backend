package execution

import (
	"exesh/internal/domain/execution/job/jobs"
)

type (
	Stage struct {
		Name StageName   `json:"name"`
		Deps []StageName `json:"deps"`
		Jobs []jobs.Job  `json:"jobs"`
	}

	StageName string
)

func (stage *Stage) BuildGraph() {
}
