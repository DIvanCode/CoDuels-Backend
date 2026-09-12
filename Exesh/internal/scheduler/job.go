package scheduler

import (
	"context"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
)

type (
	Job struct {
		jobs.Job

		Sources sourcesCallback
		OnStart startCallback
		OnDone  doneCallback
	}

	sourcesCallback func(context.Context) ([]sources.Source, error)
	startCallback   func(context.Context)
	doneCallback    func(context.Context, results.Result)
)
