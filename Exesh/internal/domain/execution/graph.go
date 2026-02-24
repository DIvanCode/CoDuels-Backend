package execution

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"sync"
)

type graph struct {
	mu sync.Mutex

	succStages    map[StageName][]*Stage
	doneStageDeps map[StageName]int

	stageByJobID map[job.ID]*Stage

	succJobs    map[job.ID][]jobs.Job
	doneJobDeps map[job.ID]int

	isActive     map[StageName]bool
	activeStages []*Stage
	toPick       map[StageName][]jobs.Job

	totalJobs map[StageName]int
	doneJobs  map[StageName]int
}

func newGraph(stages []*Stage) *graph {
	g := graph{
		mu: sync.Mutex{},

		succStages:    make(map[StageName][]*Stage),
		doneStageDeps: make(map[StageName]int),

		stageByJobID: make(map[job.ID]*Stage),

		succJobs:    make(map[job.ID][]jobs.Job),
		doneJobDeps: make(map[job.ID]int),

		isActive:     make(map[StageName]bool),
		activeStages: make([]*Stage, 0),
		toPick:       make(map[StageName][]jobs.Job),

		totalJobs: make(map[StageName]int),
		doneJobs:  make(map[StageName]int),
	}

	for _, stage := range stages {
		for _, dep := range stage.Deps {
			if _, ok := g.succStages[dep]; !ok {
				g.succStages[dep] = make([]*Stage, 0)
			}
			g.succStages[dep] = append(g.succStages[dep], stage)
		}

		g.doneStageDeps[stage.Name] = 0

		g.toPick[stage.Name] = make([]jobs.Job, 0)

		for _, jb := range stage.Jobs {
			g.stageByJobID[jb.GetID()] = stage

			deps := jb.GetDependencies()

			for _, dep := range deps {
				if _, ok := g.succJobs[dep]; !ok {
					g.succJobs[dep] = make([]jobs.Job, 0)
				}
				g.succJobs[dep] = append(g.succJobs[dep], jb)
			}

			g.doneJobDeps[jb.GetID()] = 0
			if len(deps) == 0 {
				g.toPick[stage.Name] = append(g.toPick[stage.Name], jb)
			}
		}

		g.totalJobs[stage.Name] = len(stage.Jobs)
		g.doneJobs[stage.Name] = 0

		if len(stage.Deps) == 0 {
			g.isActive[stage.Name] = true
			g.activeStages = append(g.activeStages, stage)
		}
	}

	return &g
}

func (g *graph) pickJobs() []jobs.Job {
	g.mu.Lock()
	defer g.mu.Unlock()

	pickedJobs := make([]jobs.Job, 0)
	for _, stage := range g.activeStages {
		for _, jb := range g.toPick[stage.Name] {
			pickedJobs = append(pickedJobs, jb)
		}
		g.toPick[stage.Name] = make([]jobs.Job, 0)
	}

	return pickedJobs
}

func (g *graph) doneJob(jobID job.ID, jobStatus job.Status) {
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
		for _, activeStage := range g.activeStages {
			g.isActive[activeStage.Name] = false
		}
		g.activeStages = make([]*Stage, 0)
		return
	}

	g.doneJobs[stage.Name]++
	for _, succJob := range g.succJobs[jobID] {
		g.doneJobDeps[succJob.GetID()]++
		if g.doneJobDeps[succJob.GetID()] == len(succJob.GetDependencies()) {
			succStage := g.stageByJobID[succJob.GetID()]
			g.toPick[succStage.Name] = append(g.toPick[succStage.Name], succJob)
		}
	}

	doneJobs := g.doneJobs[stage.Name]
	totalJobs := g.totalJobs[stage.Name]
	isFinished := doneJobs == totalJobs
	if isFinished {
		g.isActive[stage.Name] = false
		activeStages := make([]*Stage, 0, len(g.activeStages)-1)
		for _, activeStage := range g.activeStages {
			if activeStage.Name != stage.Name {
				activeStages = append(activeStages, activeStage)
			}
		}
		g.activeStages = activeStages
	}

	if g.canStartSuccStages(doneJobs, totalJobs) {
		for _, succStage := range g.succStages[stage.Name] {
			if g.isActive[succStage.Name] {
				continue
			}

			g.doneStageDeps[succStage.Name]++
			if g.doneStageDeps[succStage.Name] == len(succStage.Deps) {
				g.isActive[succStage.Name] = true
				g.activeStages = append(g.activeStages, succStage)
			}
		}
	}
}

func (g *graph) isDone() bool {
	g.mu.Lock()
	defer g.mu.Unlock()

	return len(g.activeStages) == 0
}

func (g *graph) canStartSuccStages(doneJobs int, totalJobs int) bool {
	return 4*doneJobs >= 3*totalJobs
}
