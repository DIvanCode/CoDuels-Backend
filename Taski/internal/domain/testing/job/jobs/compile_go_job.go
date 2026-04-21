package jobs

import (
	"taski/internal/domain/testing/input/inputs"
	"taski/internal/domain/testing/job"
)

type CompileGoJob struct {
	job.Details
	Code inputs.Input `json:"code"`
}

func NewCompileGoJob(name job.Name, categoryName string, timeLimit int, memoryLimit int, code inputs.Input) Job {
	return Job{IJob: &CompileGoJob{
		Details: job.Details{
			Type:          job.CompileGo,
			Name:          name,
			SuccessStatus: job.StatusOK,
			CategoryName:  categoryName,
			TimeLimit:     timeLimit,
			MemoryLimit:   memoryLimit,
		},
		Code: code,
	}}
}
