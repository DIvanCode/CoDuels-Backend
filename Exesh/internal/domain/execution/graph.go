package execution

import (
	"slices"
	"sync"
)

type graph struct {
	succSteps    map[StepName][]Step
	used         map[StepName]any
	topSortOrder []Step

	mu             sync.Mutex
	lastPickedStep int
	isDone         map[StepName]any
	doneSteps      int
}

func newGraph(executionSteps []Step) *graph {
	g := graph{
		succSteps: make(map[StepName][]Step),
		used:      make(map[StepName]any, len(executionSteps)),

		topSortOrder: make([]Step, 0, len(executionSteps)),

		mu:             sync.Mutex{},
		lastPickedStep: -1,
		isDone:         make(map[StepName]any),
		doneSteps:      0,
	}

	for i := len(executionSteps) - 1; i >= 0; i-- {
		step := executionSteps[i]
		for _, dep := range step.GetDependencies() {
			if _, ok := g.succSteps[dep]; !ok {
				g.succSteps[dep] = make([]Step, 0)
			}
			g.succSteps[dep] = append(g.succSteps[dep], step)
		}
	}

	g.topSort(executionSteps)
	slices.Reverse(g.topSortOrder)

	return &g
}

func (graph *graph) pickSteps() []Step {
	graph.mu.Lock()
	defer graph.mu.Unlock()

	pickedSteps := make([]Step, 0)
	for graph.lastPickedStep+1 < len(graph.topSortOrder) {
		step := graph.topSortOrder[graph.lastPickedStep+1]

		canPick := true
		for _, dep := range step.GetDependencies() {
			if _, has := graph.isDone[dep]; !has {
				canPick = false
				break
			}
		}

		if !canPick {
			break
		}

		pickedSteps = append(pickedSteps, step)
		graph.lastPickedStep++
	}
	return pickedSteps
}

func (graph *graph) doneStep(stepName StepName) {
	graph.mu.Lock()
	defer graph.mu.Unlock()

	graph.isDone[stepName] = struct{}{}
	graph.doneSteps++
}

func (graph *graph) isGraphDone() bool {
	graph.mu.Lock()
	defer graph.mu.Unlock()

	return graph.doneSteps == len(graph.topSortOrder)
}

func (g *graph) topSort(executionSteps []Step) {
	for i := len(executionSteps) - 1; i >= 0; i-- {
		step := executionSteps[i]
		if _, used := g.used[step.GetName()]; !used {
			g.dfs(step)
		}
	}
}

func (g *graph) dfs(step Step) {
	g.used[step.GetName()] = struct{}{}
	for _, succStep := range g.succSteps[step.GetName()] {
		if _, used := g.used[succStep.GetName()]; !used {
			g.dfs(succStep)
		}
	}
	g.topSortOrder = append(g.topSortOrder, step)
}
