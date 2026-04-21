package jobs

import (
	"taski/internal/domain/testing/input/inputs"
	"taski/internal/domain/testing/job"
)

type CompileCppJob struct {
	job.Details
	Code inputs.Input `json:"code"`
}

func NewCompileCppJob(name job.Name, categoryName string, timeLimit int, memoryLimit int, code inputs.Input) Job {
	return Job{IJob: &CompileCppJob{
		Details: job.Details{
			Type:          job.CompileCpp,
			Name:          name,
			SuccessStatus: job.StatusOK,
			CategoryName:  categoryName,
			TimeLimit:     timeLimit,
			MemoryLimit:   memoryLimit,
		},
		Code: code,
	}}
}
