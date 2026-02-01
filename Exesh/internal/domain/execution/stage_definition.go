package execution

import "exesh/internal/domain/execution/job/jobs"

type StageDefinition struct {
	Name StageName         `json:"name"`
	Deps []StageName       `json:"deps"`
	Jobs []jobs.Definition `json:"jobs"`
}
