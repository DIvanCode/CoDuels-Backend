package execution

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
)

func (ex *Execution) optimizeChains() {
	jobByID := make(map[job.ID]jobs.Job)
	stageByJobID := make(map[job.ID]StageName)
	succJobs := make(map[job.ID][]jobs.Job)

	for _, stage := range ex.Stages {
		for _, jb := range stage.Jobs {
			jobID := jb.GetID()
			jobByID[jobID] = jb
			stageByJobID[jobID] = stage.Name
			succJobs[jobID] = nil
		}
	}

	for _, stage := range ex.Stages {
		for _, jb := range stage.Jobs {
			for _, depID := range jb.GetDependencies() {
				if _, ok := jobByID[depID]; !ok {
					continue
				}
				succJobs[depID] = append(succJobs[depID], jb)
			}
		}
	}

	for _, stage := range ex.Stages {
		stage.Jobs = optimizeStageChains(stage.Name, stage.Jobs, stageByJobID, succJobs)
	}
}

func optimizeStageChains(
	stageName StageName,
	stageJobs []jobs.Job,
	stageByJobID map[job.ID]StageName,
	succJobs map[job.ID][]jobs.Job,
) []jobs.Job {
	inStagePredCount := make(map[job.ID]int, len(stageJobs))
	for _, jb := range stageJobs {
		inStagePredCount[jb.GetID()] = 0
	}

	for _, jb := range stageJobs {
		jobID := jb.GetID()
		for _, succ := range succJobs[jobID] {
			if stageByJobID[succ.GetID()] != stageName {
				continue
			}
			inStagePredCount[succ.GetID()]++
		}
	}

	replacementByTailID := make(map[job.ID]jobs.Job)
	chained := make(map[job.ID]struct{})

	for _, jb := range stageJobs {
		jobID := jb.GetID()
		if _, ok := chained[jobID]; ok {
			continue
		}
		if inStagePredCount[jobID] != 0 {
			continue
		}

		chainJobs := collectChainJobs(stageName, jb, stageByJobID, succJobs, inStagePredCount, chained)
		if len(chainJobs) < 2 {
			continue
		}

		tailID := chainJobs[len(chainJobs)-1].GetID()
		replacementByTailID[tailID] = jobs.NewChainJob(tailID, chainJobs[len(chainJobs)-1].GetSuccessStatus(), chainJobs)
		for _, chainJob := range chainJobs {
			chained[chainJob.GetID()] = struct{}{}
		}
	}

	optimized := make([]jobs.Job, 0, len(stageJobs))
	for _, jb := range stageJobs {
		if chainJob, ok := replacementByTailID[jb.GetID()]; ok {
			optimized = append(optimized, chainJob)
			continue
		}
		if _, ok := chained[jb.GetID()]; ok {
			continue
		}
		optimized = append(optimized, jb)
	}

	return optimized
}

func collectChainJobs(
	stageName StageName,
	head jobs.Job,
	stageByJobID map[job.ID]StageName,
	succJobs map[job.ID][]jobs.Job,
	inStagePredCount map[job.ID]int,
	chained map[job.ID]struct{},
) []jobs.Job {
	chainJobs := []jobs.Job{head}
	current := head

	for {
		successors := succJobs[current.GetID()]
		if len(successors) != 1 {
			break
		}

		next := successors[0]
		nextID := next.GetID()
		if stageByJobID[nextID] != stageName {
			break
		}
		if next.GetType() == job.Chain {
			break
		}
		if inStagePredCount[nextID] != 1 {
			break
		}
		if _, ok := chained[nextID]; ok {
			break
		}

		chainJobs = append(chainJobs, next)
		current = next
	}

	return chainJobs
}
