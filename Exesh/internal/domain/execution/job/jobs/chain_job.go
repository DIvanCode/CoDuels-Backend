package jobs

import (
	"exesh/internal/domain/execution/input"
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/output"
	"exesh/internal/domain/execution/source"
)

type ChainJob struct {
	job.Details
	Jobs []Job `json:"jobs"`
}

func NewChainJob(jobs []Job) Job {
	if len(jobs) == 0 {
		panic("empty chain jobs")
	}

	lastJob := jobs[len(jobs)-1]
	return Job{
		&ChainJob{
			Details: job.Details{
				ID:            lastJob.GetID(),
				Type:          job.Chain,
				SuccessStatus: lastJob.GetSuccessStatus(),
			},
			Jobs: jobs,
		},
	}
}

func (jb *ChainJob) GetInputs() []input.Input {
	innerJobs := make(map[job.ID]any, len(jb.Jobs))
	for _, innerJob := range jb.Jobs {
		innerJobs[innerJob.GetID()] = struct{}{}
	}

	inputs := make(map[source.ID]input.Input, len(jb.Jobs))
	for _, innerJob := range jb.Jobs {
		for _, in := range innerJob.GetInputs() {
			if in.Type == input.Artifact {
				var inputJobID job.ID
				if err := inputJobID.FromString(in.SourceID.String()); err == nil {
					if _, ok := innerJobs[inputJobID]; ok {
						// inner job artifact is not input for chain job
						continue
					}
				}
			}

			if _, ok := inputs[in.SourceID]; ok {
				continue
			}

			inputs[in.SourceID] = in
		}
	}

	inputsList := make([]input.Input, 0, len(inputs))
	for _, in := range inputs {
		inputsList = append(inputsList, in)
	}
	return inputsList
}

func (jb *ChainJob) GetOutput() *output.Output {
	if len(jb.Jobs) == 0 {
		return nil
	}

	lastJob := jb.Jobs[len(jb.Jobs)-1]
	return lastJob.GetOutput()
}

func (jb *ChainJob) GetDependencies() []job.ID {
	return getDependencies(jb.GetInputs())
}
