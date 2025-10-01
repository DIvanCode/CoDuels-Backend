package execution

import (
	"crypto/sha1"
	"fmt"

	"github.com/DIvanCode/filestorage/pkg/bucket"
)

type Context struct {
	ExecutionID ID

	InlineSourcesBucketID bucket.ID

	graph *graph

	stepByJobID   map[JobID]Step
	jobByStepName map[StepName]Job
}

func newContext(executionID ID, graph *graph) (ctx Context, err error) {
	ctx = Context{
		ExecutionID: executionID,

		graph: graph,

		stepByJobID:   make(map[JobID]Step),
		jobByStepName: make(map[StepName]Job),
	}

	if err = ctx.InlineSourcesBucketID.FromString(string(sha1.New().Sum([]byte(executionID.String())))); err != nil {
		err = fmt.Errorf("failed to create inline sources bucket id: %w", err)
		return
	}

	return
}

func (c *Context) PickSteps() []Step {
	return c.graph.pickSteps()
}

func (c *Context) ScheduledStep(step Step, job Job) {
	c.stepByJobID[job.GetID()] = step
	c.jobByStepName[step.GetName()] = job
}

func (c *Context) DoneStep(stepName StepName) {
	c.graph.doneStep(stepName)
}

func (c *Context) IsDone() bool {
	return c.graph.isDone()
}

func (c *Context) GetJobForStep(stepName StepName) (Job, bool) {
	job, ok := c.jobByStepName[stepName]
	return job, ok
}
