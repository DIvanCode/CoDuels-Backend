package heartbeat

import (
	"exesh/internal/api"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
)

type (
	Request struct {
		WorkerID        string           `json:"worker_id"`
		DoneJobs        []results.Result `json:"done_jobs"`
		FreeSlots       int              `json:"free_slots"`
		AvailableMemory int              `json:"available_memory_mb"`
	}

	Response struct {
		api.Response
		Jobs    []jobs.Job       `json:"jobs,omitempty"`
		Sources []sources.Source `json:"sources,omitempty"`
	}
)
