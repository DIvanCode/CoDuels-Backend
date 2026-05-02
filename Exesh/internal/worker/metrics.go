package worker

import (
	"errors"

	"github.com/prometheus/client_golang/prometheus"
)

type workerMetricsSnapshot struct {
	totalSlots          int
	totalMemory         int
	freeSlots           int
	availableMemory     int
	queuedJobs          int
	queuedJobsMemory    int
	doneJobsPendingSend int
}

func (w *Worker) RegisterMetrics(r prometheus.Registerer) error {
	labels := prometheus.Labels{"worker_id": w.cfg.WorkerID}
	return errors.Join(
		r.Register(prometheus.NewGaugeFunc(prometheus.GaugeOpts{
			Name:        "total_slots",
			Help:        "Total worker slots from worker config.",
			ConstLabels: labels,
		}, func() float64 {
			return float64(w.snapshotMetrics().totalSlots)
		})),
		r.Register(prometheus.NewGaugeFunc(prometheus.GaugeOpts{
			Name:        "total_memory_mb",
			Help:        "Total worker memory in MiB from worker config.",
			ConstLabels: labels,
		}, func() float64 {
			return float64(w.snapshotMetrics().totalMemory)
		})),
		r.Register(prometheus.NewGaugeFunc(prometheus.GaugeOpts{
			Name:        "free_slots",
			Help:        "Currently free worker slots.",
			ConstLabels: labels,
		}, func() float64 {
			return float64(w.snapshotMetrics().freeSlots)
		})),
		r.Register(prometheus.NewGaugeFunc(prometheus.GaugeOpts{
			Name:        "available_memory_mb",
			Help:        "Currently available worker memory in MiB.",
			ConstLabels: labels,
		}, func() float64 {
			return float64(w.snapshotMetrics().availableMemory)
		})),
		r.Register(prometheus.NewGaugeFunc(prometheus.GaugeOpts{
			Name:        "queued_jobs",
			Help:        "Jobs accepted from coordinator but not yet started by a worker goroutine.",
			ConstLabels: labels,
		}, func() float64 {
			return float64(w.snapshotMetrics().queuedJobs)
		})),
		r.Register(prometheus.NewGaugeFunc(prometheus.GaugeOpts{
			Name:        "queued_jobs_expected_memory_mb",
			Help:        "Expected memory in MiB reserved by jobs queued locally on worker.",
			ConstLabels: labels,
		}, func() float64 {
			return float64(w.snapshotMetrics().queuedJobsMemory)
		})),
		r.Register(prometheus.NewGaugeFunc(prometheus.GaugeOpts{
			Name:        "done_jobs_pending_heartbeat",
			Help:        "Completed job results waiting to be sent in the next heartbeat.",
			ConstLabels: labels,
		}, func() float64 {
			return float64(w.snapshotMetrics().doneJobsPendingSend)
		})),
	)
}

func (w *Worker) snapshotMetrics() workerMetricsSnapshot {
	w.mu.Lock()
	defer w.mu.Unlock()

	return workerMetricsSnapshot{
		totalSlots:          w.cfg.FreeSlots,
		totalMemory:         w.cfg.AvailableMemory,
		freeSlots:           w.freeSlots - w.jobs.Size(),
		availableMemory:     w.availableMemory - w.jobsExpectedTotalMemory,
		queuedJobs:          w.jobs.Size(),
		queuedJobsMemory:    w.jobsExpectedTotalMemory,
		doneJobsPendingSend: len(w.doneJobs),
	}
}
