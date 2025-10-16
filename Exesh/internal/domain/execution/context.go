package execution

import (
	"crypto/sha1"
	"fmt"
	"sync"

	"github.com/DIvanCode/filestorage/pkg/bucket"
)

type Context struct {
	ExecutionID ID

	InlineSourcesBucketID bucket.ID

	graph *graph

	stepByJobID   map[JobID]Step
	jobByStepName map[StepName]Job

	mu     *sync.Mutex
	failed bool
}

func newContext(executionID ID, graph *graph) (ctx Context, err error) {
	ctx = Context{
		ExecutionID: executionID,

		graph: graph,

		stepByJobID:   make(map[JobID]Step),
		jobByStepName: make(map[StepName]Job),

		mu:     &sync.Mutex{},
		failed: false,
	}

	hash := sha1.New()
	hash.Write([]byte(executionID.String()))
	if err = ctx.InlineSourcesBucketID.FromString(fmt.Sprintf("%x", hash.Sum(nil))); err != nil {
		err = fmt.Errorf("failed to create inline sources bucket id: %w", err)
		return
	}

	return
}

func (c *Context) PickSteps() []Step {
	if c.isFailed() {
		return []Step{}
	}
	return c.graph.pickSteps()
}

func (c *Context) ScheduledStep(step Step, job Job) {
	c.stepByJobID[job.GetID()] = step
	c.jobByStepName[step.GetName()] = job
}

func (c *Context) FailStep(stepName StepName) {
	c.mu.Lock()
	defer c.mu.Unlock()
	c.failed = true
}

func (c *Context) DoneStep(stepName StepName) {
	c.graph.doneStep(stepName)
}

func (c *Context) IsDone() bool {
	if c.isFailed() {
		return true
	}
	return c.graph.isGraphDone()
}

func (c *Context) GetJobForStep(stepName StepName) (Job, bool) {
	job, ok := c.jobByStepName[stepName]
	return job, ok
}

func (c *Context) isFailed() bool {
	c.mu.Lock()
	defer c.mu.Unlock()
	return c.failed
}
