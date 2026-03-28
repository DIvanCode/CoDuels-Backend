package jobs

import (
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/output"
)

type ChainJob struct {
	job.Details
	Jobs []Job `json:"jobs"`
}

func NewChainJob(
	id job.ID,
	successStatus job.Status,
	chainJobs []Job,
) Job {
	return Job{
		&ChainJob{
			Details: job.Details{
				ID:            id,
				Type:          job.Chain,
				SuccessStatus: successStatus,
			},
			Jobs: chainJobs,
		},
	}
}

func (jb *ChainJob) GetInputs() []input.Input {
	internalJobIDs := make(map[string]struct{}, len(jb.Jobs))
	for _, chainJob := range jb.Jobs {
		chainJobID := chainJob.GetID()
		internalJobIDs[chainJobID.String()] = struct{}{}
	}

	seen := make(map[string]struct{})
	inputs := make([]input.Input, 0)
	for _, chainJob := range jb.Jobs {
		for _, in := range chainJob.GetInputs() {
			if in.Type == input.Artifact {
				if _, ok := internalJobIDs[in.SourceID.String()]; ok {
					continue
				}
			}
			key := string(in.Type) + ":" + in.SourceID.String()
			if _, ok := seen[key]; ok {
				continue
			}
			seen[key] = struct{}{}
			inputs = append(inputs, in)
		}
	}

	return inputs
}

func (jb *ChainJob) GetOutput() *output.Output {
	if len(jb.Jobs) == 0 {
		return nil
	}
	return jb.Jobs[len(jb.Jobs)-1].GetOutput()
}

func (jb *ChainJob) GetDependencies() []job.ID {
	internalJobIDs := make(map[string]struct{}, len(jb.Jobs))
	for _, chainJob := range jb.Jobs {
		chainJobID := chainJob.GetID()
		internalJobIDs[chainJobID.String()] = struct{}{}
	}

	seen := make(map[string]struct{})
	deps := make([]job.ID, 0)
	for _, chainJob := range jb.Jobs {
		for _, dep := range chainJob.GetDependencies() {
			if _, ok := internalJobIDs[dep.String()]; ok {
				continue
			}
			if _, ok := seen[dep.String()]; ok {
				continue
			}
			seen[dep.String()] = struct{}{}
			deps = append(deps, dep)
		}
	}

	return deps
}
