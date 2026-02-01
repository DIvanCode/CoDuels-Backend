package execution

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"sync"
)

type stagesGraph struct {
	mu sync.Mutex

	succStages map[StageName][]*Stage
	doneDeps   map[StageName]int

	toPick []*Stage

	jobGraphs    map[StageName]*jobsGraph
	stageByJobID map[job.ID]*Stage
}

func newStagesGraph(stages []*Stage) *stagesGraph {
	g := stagesGraph{
		mu: sync.Mutex{},

		succStages: make(map[StageName][]*Stage),
		doneDeps:   make(map[StageName]int),

		toPick: make([]*Stage, 0),

		jobGraphs:    make(map[StageName]*jobsGraph),
		stageByJobID: make(map[job.ID]*Stage),
	}

	for _, stage := range stages {
		for _, dep := range stage.Deps {
			if _, ok := g.succStages[dep]; !ok {
				g.succStages[dep] = make([]*Stage, 0)
			}
			g.succStages[dep] = append(g.succStages[dep], stage)
		}

		g.doneDeps[stage.Name] = 0
		if len(stage.Deps) == 0 {
			g.toPick = append(g.toPick, stage)
		}

		g.jobGraphs[stage.Name] = newJobsGraph(stage.Jobs)
		for _, jb := range stage.Jobs {
			g.stageByJobID[jb.GetID()] = stage
		}
	}

	return &g
}

func (g *stagesGraph) pickJobs() []jobs.Job {
	g.mu.Lock()
	defer g.mu.Unlock()

	pickedJobs := make([]jobs.Job, 0)
	for _, stage := range g.toPick {
		pickedJobs = append(pickedJobs, g.jobGraphs[stage.Name].pickJobs()...)
	}

	return pickedJobs
}

func (g *stagesGraph) doneJob(jobID job.ID, jobStatus job.Status) {
	g.mu.Lock()
	defer g.mu.Unlock()

	stage := g.stageByJobID[jobID]

	var jb *jobs.Job
	for i := range stage.Jobs {
		if stage.Jobs[i].GetID() == jobID {
			jb = &stage.Jobs[i]
			break
		}
	}
	if jb == nil {
		return
	}

	if jobStatus != jb.GetSuccessStatus() {
		g.toPick = make([]*Stage, 0)
		return
	}

	g.jobGraphs[stage.Name].doneJob(jobID)
	if g.jobGraphs[stage.Name].isDone() {
		toPick := make([]*Stage, 0)
		for i := range g.toPick {
			if g.toPick[i].Name != stage.Name {
				toPick = append(toPick, g.toPick[i])
			}
		}

		for _, succStage := range g.succStages[stage.Name] {
			g.doneDeps[succStage.Name]++
			if g.doneDeps[succStage.Name] == len(succStage.Deps) {
				toPick = append(toPick, succStage)
			}
		}

		g.toPick = toPick
	}
}

func (g *stagesGraph) isDone() bool {
	g.mu.Lock()
	defer g.mu.Unlock()

	return len(g.toPick) == 0
}
