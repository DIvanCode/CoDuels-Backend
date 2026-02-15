package execution

import (
	"taski/internal/domain/testing/job/jobs"
)

type (
	ID string

	Stage struct {
		Name StageName   `json:"name"`
		Deps []StageName `json:"deps"`
		Jobs []jobs.Job  `json:"jobs"`
	}

	StageName string

	Stages []Stage
)
