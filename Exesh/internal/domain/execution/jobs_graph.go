package execution

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"sync"
)

type jobsGraph struct {
	mu sync.Mutex

	succJobs map[job.ID][]jobs.Job
	doneDeps map[job.ID]int

	toPick []jobs.Job

	totalJobs int
	doneJobs  int
}

func newJobsGraph(jbs []jobs.Job) *jobsGraph {
	g := jobsGraph{
		mu: sync.Mutex{},

		succJobs: make(map[job.ID][]jobs.Job),
		doneDeps: make(map[job.ID]int),

		toPick: make([]jobs.Job, 0),

		totalJobs: len(jbs),
		doneJobs:  0,
	}

	for _, jb := range jbs {
		deps := jb.GetDependencies()

		for _, dep := range deps {
			if _, ok := g.succJobs[dep]; !ok {
				g.succJobs[dep] = make([]jobs.Job, 0)
			}
			g.succJobs[dep] = append(g.succJobs[dep], jb)
		}

		g.doneDeps[jb.GetID()] = 0
		if len(deps) == 0 {
			g.toPick = append(g.toPick, jb)
		}
	}

	return &g
}

func (g *jobsGraph) pickJobs() []jobs.Job {
	g.mu.Lock()
	defer g.mu.Unlock()

	pickedJobs := make([]jobs.Job, 0, len(g.toPick))
	copy(pickedJobs, g.toPick)
	g.toPick = make([]jobs.Job, 0)

	return pickedJobs
}

func (g *jobsGraph) doneJob(jobID job.ID) {
	g.mu.Lock()
	defer g.mu.Unlock()

	g.doneJobs++
	for _, succJob := range g.succJobs[jobID] {
		g.doneDeps[succJob.GetID()]++
		if g.doneDeps[succJob.GetID()] == len(succJob.GetDependencies()) {
			g.toPick = append(g.toPick, succJob)
		}
	}
}

func (g *jobsGraph) isDone() bool {
	g.mu.Lock()
	defer g.mu.Unlock()

	return g.doneJobs == g.totalJobs
}
