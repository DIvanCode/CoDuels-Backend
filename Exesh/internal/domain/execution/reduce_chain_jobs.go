package execution

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
)

func reduceStageJobs(stageJobs []jobs.Job) []jobs.Job {
	if len(stageJobs) <= 1 {
		return stageJobs
	}

	jobByID := make(map[job.ID]jobs.Job, len(stageJobs))
	for _, jb := range stageJobs {
		jobByID[jb.GetID()] = jb
	}

	successors := make(map[job.ID][]job.ID, len(stageJobs))
	for _, jb := range stageJobs {
		successors[jb.GetID()] = make([]job.ID, 0)
	}

	for _, jb := range stageJobs {
		for _, dep := range jb.GetDependencies() {
			if _, ok := jobByID[dep]; ok {
				successors[dep] = append(successors[dep], jb.GetID())
			}
		}
	}

	removed := make(map[job.ID]bool, len(stageJobs))
	reduced := make([]jobs.Job, 0, len(stageJobs))

	for _, jb := range stageJobs {
		if removed[jb.GetID()] {
			continue
		}

		innerJobs := make([]jobs.Job, 0, 2)
		if jb.GetType() == job.Chain {
			innerJobs = append(innerJobs, jb.AsChain().Jobs...)
		} else {
			innerJobs = append(innerJobs, jb)
		}

		currentID := jb.GetID()
		for {
			nextID, ok := findSingleAliveSuccessor(successors[currentID], removed)
			if !ok {
				break
			}

			nextJob := jobByID[nextID]
			if nextJob.GetType() == job.Chain {
				innerJobs = append(innerJobs, nextJob.AsChain().Jobs...)
			} else {
				innerJobs = append(innerJobs, nextJob)
			}
			removed[nextID] = true
			currentID = nextID
		}

		if len(innerJobs) == 1 {
			reduced = append(reduced, innerJobs[0])
			continue
		}

		reduced = append(reduced, jobs.NewChainJob(innerJobs))
	}

	return reduced
}

func findSingleAliveSuccessor(successors []job.ID, removed map[job.ID]bool) (job.ID, bool) {
	var successor job.ID
	aliveCnt := 0
	for _, succID := range successors {
		if removed[succID] {
			continue
		}
		successor = succID
		aliveCnt++
		if aliveCnt > 1 {
			return successor, false
		}
	}
	return successor, aliveCnt == 1
}
