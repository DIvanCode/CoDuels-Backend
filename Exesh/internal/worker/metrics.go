package worker

import (
	"errors"
	"time"

	"github.com/prometheus/client_golang/prometheus"
)

type workerRuntimeMetrics struct {
	workerID string

	jobStartedTotal          *prometheus.CounterVec
	jobFinishedTotal         *prometheus.CounterVec
	slotOccupiedSecondsTotal prometheus.Counter
	memoryOccupiedMBSeconds  prometheus.Counter
}

type workerMetricsSnapshot struct {
	totalSlots          int
	totalMemory         int
	freeSlots           int
	availableMemory     int
	queuedJobs          int
	queuedJobsMemory    int
	doneJobsPendingSend int
}

func newWorkerRuntimeMetrics(workerID string) *workerRuntimeMetrics {
	labels := prometheus.Labels{"worker_id": workerID}
	m := &workerRuntimeMetrics{
		workerID: workerID,
		jobStartedTotal: prometheus.NewCounterVec(prometheus.CounterOpts{
			Name: "worker_job_started_total",
			Help: "Total number of jobs started by this worker.",
		}, []string{"worker_id"}),
		jobFinishedTotal: prometheus.NewCounterVec(prometheus.CounterOpts{
			Name: "worker_job_finished_total",
			Help: "Total number of jobs finished by this worker, labeled by result status.",
		}, []string{"worker_id", "status"}),
		slotOccupiedSecondsTotal: prometheus.NewCounter(prometheus.CounterOpts{
			Name:        "worker_slot_occupied_seconds_total",
			Help:        "Total slot-seconds occupied by jobs on this worker.",
			ConstLabels: labels,
		}),
		memoryOccupiedMBSeconds: prometheus.NewCounter(prometheus.CounterOpts{
			Name:        "worker_memory_occupied_mb_seconds_total",
			Help:        "Total MiB-seconds occupied by jobs on this worker, based on expected job memory.",
			ConstLabels: labels,
		}),
	}
	m.jobStartedTotal.WithLabelValues(workerID)
	for _, status := range []string{"OK", "CE", "RE", "TL", "ML", "WA"} {
		m.jobFinishedTotal.WithLabelValues(workerID, status)
	}
	return m
}

func (w *Worker) RegisterMetrics(r prometheus.Registerer) error {
	labels := prometheus.Labels{"worker_id": w.cfg.WorkerID}
	return errors.Join(
		r.Register(w.metrics.jobStartedTotal),
		r.Register(w.metrics.jobFinishedTotal),
		r.Register(w.metrics.slotOccupiedSecondsTotal),
		r.Register(w.metrics.memoryOccupiedMBSeconds),
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

func (m *workerRuntimeMetrics) jobStarted() {
	m.jobStartedTotal.WithLabelValues(m.workerID).Inc()
}

func (m *workerRuntimeMetrics) jobFinished(status string, duration time.Duration, expectedMemoryMB int) {
	seconds := duration.Seconds()
	m.jobFinishedTotal.WithLabelValues(m.workerID, status).Inc()
	m.slotOccupiedSecondsTotal.Add(seconds)
	m.memoryOccupiedMBSeconds.Add(seconds * float64(expectedMemoryMB))
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
