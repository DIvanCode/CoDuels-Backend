package execution

import (
	"exesh/internal/domain/execution/job"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/output"
	"exesh/internal/domain/execution/source"
	"exesh/internal/domain/execution/source/sources"
	"sync"
)

type (
	Execution struct {
		Definition

		Stages []*Stage

		JobByName         map[job.DefinitionName]jobs.Job
		JobDefinitionByID map[job.ID]jobs.Definition

		SourceDefinitionByName map[source.DefinitionName]sources.Definition
		SourceByID             map[source.ID]sources.Source

		OutputByJob map[job.ID]output.Output

		graph *stagesGraph

		mu          sync.Mutex
		forceFailed bool
	}
)

func NewExecution(def Definition) *Execution {
	ex := Execution{
		Definition: def,
		Stages:     make([]*Stage, len(def.Stages)),

		JobByName:         make(map[job.DefinitionName]jobs.Job),
		JobDefinitionByID: make(map[job.ID]jobs.Definition),

		SourceDefinitionByName: make(map[source.DefinitionName]sources.Definition),
		SourceByID:             make(map[source.ID]sources.Source),

		OutputByJob: make(map[job.ID]output.Output),
	}

	return &ex
}

func (ex *Execution) BuildGraph() {
	ex.graph = newStagesGraph(ex.Stages)
}

func (ex *Execution) PickJobs() []jobs.Job {
	if ex.IsDone() {
		return []jobs.Job{}
	}

	return ex.graph.pickJobs()
}

func (ex *Execution) DoneJob(jobID job.ID, jobStatus job.Status) {
	ex.graph.doneJob(jobID, jobStatus)
}

func (ex *Execution) IsDone() bool {
	return ex.IsForceFailed() || ex.graph.isDone()
}

func (ex *Execution) ForceFail() {
	ex.mu.Lock()
	defer ex.mu.Unlock()

	ex.forceFailed = true
}

func (ex *Execution) IsForceFailed() bool {
	ex.mu.Lock()
	defer ex.mu.Unlock()

	return ex.forceFailed
}
