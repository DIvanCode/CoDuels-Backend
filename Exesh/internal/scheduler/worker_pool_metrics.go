package scheduler

import (
	"exesh/internal/domain/execution/job"
	"time"

	"github.com/prometheus/client_golang/prometheus"
)

type workerPoolCollector struct {
	pool *WorkerPool

	registeredDesc      *prometheus.Desc
	totalSlotsDesc      *prometheus.Desc
	totalMemoryDesc     *prometheus.Desc
	runningJobsDesc     *prometheus.Desc
	usedMemoryDesc      *prometheus.Desc
	freeSlotsDesc       *prometheus.Desc
	freeMemoryDesc      *prometheus.Desc
	heartbeatAgeDesc    *prometheus.Desc
	registeredTotalDesc *prometheus.Desc
}

func newWorkerPoolCollector(pool *WorkerPool) *workerPoolCollector {
	labels := []string{"worker_id"}
	return &workerPoolCollector{
		pool: pool,
		registeredDesc: prometheus.NewDesc(
			"worker_pool_worker_registered",
			"Registered worker presence in WorkerPool. Value is 1 while registered.",
			labels, nil,
		),
		totalSlotsDesc: prometheus.NewDesc(
			"worker_pool_worker_total_slots",
			"Total slots reported by worker on registration.",
			labels, nil,
		),
		totalMemoryDesc: prometheus.NewDesc(
			"worker_pool_worker_total_memory_mb",
			"Total memory in MiB reported by worker on registration.",
			labels, nil,
		),
		runningJobsDesc: prometheus.NewDesc(
			"worker_pool_worker_running_jobs",
			"Number of jobs tracked as running on worker by coordinator.",
			labels, nil,
		),
		usedMemoryDesc: prometheus.NewDesc(
			"worker_pool_worker_used_expected_memory_mb",
			"Expected memory in MiB reserved by jobs tracked as running on worker.",
			labels, nil,
		),
		freeSlotsDesc: prometheus.NewDesc(
			"worker_pool_worker_free_expected_slots",
			"Free slots according to coordinator WorkerPool state.",
			labels, nil,
		),
		freeMemoryDesc: prometheus.NewDesc(
			"worker_pool_worker_free_expected_memory_mb",
			"Free memory in MiB according to coordinator WorkerPool state.",
			labels, nil,
		),
		heartbeatAgeDesc: prometheus.NewDesc(
			"worker_pool_worker_last_heartbeat_age_seconds",
			"Seconds since last heartbeat seen by coordinator.",
			labels, nil,
		),
		registeredTotalDesc: prometheus.NewDesc(
			"worker_pool_registered_workers",
			"Total registered workers in coordinator WorkerPool.",
			nil, nil,
		),
	}
}

func (c *workerPoolCollector) Describe(ch chan<- *prometheus.Desc) {
	ch <- c.registeredDesc
	ch <- c.totalSlotsDesc
	ch <- c.totalMemoryDesc
	ch <- c.runningJobsDesc
	ch <- c.usedMemoryDesc
	ch <- c.freeSlotsDesc
	ch <- c.freeMemoryDesc
	ch <- c.heartbeatAgeDesc
	ch <- c.registeredTotalDesc
}

func (c *workerPoolCollector) Collect(ch chan<- prometheus.Metric) {
	workers := c.pool.getWorkersMetricsSnapshot()
	now := time.Now()

	ch <- prometheus.MustNewConstMetric(c.registeredTotalDesc, prometheus.GaugeValue, float64(len(workers)))
	for _, w := range workers {
		workerID := w.ID
		runningJobs := len(w.RunningJobs)
		freeSlots := w.Slots - runningJobs
		freeMemory := w.Memory - w.RunningJobsTotalExpectedMemory

		ch <- prometheus.MustNewConstMetric(c.registeredDesc, prometheus.GaugeValue, 1, workerID)
		ch <- prometheus.MustNewConstMetric(c.totalSlotsDesc, prometheus.GaugeValue, float64(w.Slots), workerID)
		ch <- prometheus.MustNewConstMetric(c.totalMemoryDesc, prometheus.GaugeValue, float64(w.Memory), workerID)
		ch <- prometheus.MustNewConstMetric(c.runningJobsDesc, prometheus.GaugeValue, float64(runningJobs), workerID)
		ch <- prometheus.MustNewConstMetric(c.usedMemoryDesc, prometheus.GaugeValue, float64(w.RunningJobsTotalExpectedMemory), workerID)
		ch <- prometheus.MustNewConstMetric(c.freeSlotsDesc, prometheus.GaugeValue, float64(freeSlots), workerID)
		ch <- prometheus.MustNewConstMetric(c.freeMemoryDesc, prometheus.GaugeValue, float64(freeMemory), workerID)
		ch <- prometheus.MustNewConstMetric(c.heartbeatAgeDesc, prometheus.GaugeValue, now.Sub(w.LastHeartbeat).Seconds(), workerID)
	}
}

func (p *WorkerPool) RegisterMetrics(r prometheus.Registerer) error {
	return r.Register(newWorkerPoolCollector(p))
}

func (p *WorkerPool) getWorkersMetricsSnapshot() []*worker {
	p.mu.Lock()
	defer p.mu.Unlock()

	workers := make([]*worker, 0, len(p.workers))
	for _, w := range p.workers {
		cp := &worker{
			ID:                             w.ID,
			Slots:                          w.Slots,
			Memory:                         w.Memory,
			LastHeartbeat:                  w.LastHeartbeat,
			Artifacts:                      nil,
			RunningJobs:                    make(map[job.ID]runningJob, len(w.RunningJobs)),
			RunningJobsTotalExpectedMemory: w.RunningJobsTotalExpectedMemory,
		}
		for jobID, jb := range w.RunningJobs {
			cp.RunningJobs[jobID] = jb
		}
		workers = append(workers, cp)
	}
	return workers
}

var _ prometheus.Collector = (*workerPoolCollector)(nil)
