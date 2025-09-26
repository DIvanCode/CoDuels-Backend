package graph

import (
	"fmt"
	"slices"
	"sync"

	"github.com/google/uuid"
)

type (
	Graph struct {
		ID ID

		succJobs     map[JobID][]Job
		topSortOrder []Job

		mu            sync.Mutex
		lastPickedJob int
		isDone        map[JobID]any
	}

	ID uuid.UUID
)

func (id ID) String() string {
	uid := uuid.UUID(id)
	return uid.String()
}

func NewGraph(jobs []Job) *Graph {
	graph := Graph{
		ID: ID(uuid.New()),

		succJobs:     make(map[JobID][]Job),
		topSortOrder: make([]Job, 0, len(jobs)),

		mu:            sync.Mutex{},
		lastPickedJob: -1,
		isDone:        make(map[JobID]any),
	}

	for i := len(jobs) - 1; i >= 0; i-- {
		job := jobs[i]
		for _, dep := range job.GetDependencies() {
			if _, ok := graph.succJobs[dep]; !ok {
				graph.succJobs[dep] = make([]Job, 0)
			}
			graph.succJobs[dep] = append(graph.succJobs[dep], job)
		}
	}

	for _, job := range jobs {
		fmt.Printf("%v\n", job)
	}

	graph.topSort(jobs)
	slices.Reverse(graph.topSortOrder)

	return &graph
}

func (graph *Graph) IsDone() bool {
	graph.mu.Lock()
	defer graph.mu.Unlock()

	return graph.lastPickedJob == len(graph.topSortOrder)-1
}

func (graph *Graph) PickJobs() []Job {
	graph.mu.Lock()
	defer graph.mu.Unlock()

	jobs := make([]Job, 0)
	for graph.lastPickedJob+1 < len(graph.topSortOrder) {
		job := graph.topSortOrder[graph.lastPickedJob+1]

		canPick := true
		for _, dep := range job.GetDependencies() {
			if _, ok := graph.isDone[dep]; !ok {
				canPick = false
				break
			}
		}
		if !canPick {
			break
		}

		jobs = append(jobs, job)
		graph.lastPickedJob++
	}
	return jobs
}

func (graph *Graph) DoneJob(jobID JobID) {
	graph.mu.Lock()
	defer graph.mu.Unlock()

	graph.isDone[jobID] = struct{}{}
}

func (graph *Graph) topSort(initOrder []Job) {
	used := make(map[JobID]any, len(initOrder))
	for i := len(initOrder) - 1; i >= 0; i-- {
		job := initOrder[i]
		if _, ok := used[job.GetID()]; !ok {
			dfs(job, graph, used)
		}
	}
}

func dfs(job Job, graph *Graph, used map[JobID]any) {
	used[job.GetID()] = struct{}{}
	for _, succJob := range graph.succJobs[job.GetID()] {
		if _, ok := used[succJob.GetID()]; !ok {
			dfs(succJob, graph, used)
		}
	}
	graph.topSortOrder = append(graph.topSortOrder, job)
}
