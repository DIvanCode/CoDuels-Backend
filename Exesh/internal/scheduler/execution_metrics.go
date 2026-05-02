package scheduler

import (
	"errors"
	"time"

	"github.com/prometheus/client_golang/prometheus"
)

type executionSchedulerCollector struct {
	scheduler *ExecutionScheduler

	activeExecutionsDesc       *prometheus.Desc
	executionPriorityDesc      *prometheus.Desc
	executionProgressRatioDesc *prometheus.Desc
	executionProgressDesc      *prometheus.Desc
	executionTotalExpectedDesc *prometheus.Desc
	executionDoneExpectedDesc  *prometheus.Desc
	executionRetriesDesc       *prometheus.Desc
}

type executionAggregateMetrics struct {
	startedTotal          prometheus.Counter
	finishedTotal         *prometheus.CounterVec
	durationSeconds       prometheus.Histogram
	priorityOnPick        prometheus.Histogram
	progressRatioOnPick   prometheus.Histogram
	progressRatioOnFinish prometheus.Histogram
	schedulerPickTotal    prometheus.Counter
}

func newExecutionAggregateMetrics() *executionAggregateMetrics {
	return &executionAggregateMetrics{
		startedTotal: prometheus.NewCounter(prometheus.CounterOpts{
			Name: "execution_started_total",
			Help: "Total number of executions started by ExecutionScheduler.",
		}),
		finishedTotal: prometheus.NewCounterVec(prometheus.CounterOpts{
			Name: "execution_finished_total",
			Help: "Total number of executions finished by ExecutionScheduler.",
		}, []string{"status"}),
		durationSeconds: prometheus.NewHistogram(prometheus.HistogramOpts{
			Name:    "execution_duration_seconds",
			Help:    "Execution duration in seconds.",
			Buckets: []float64{0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 120, 300, 600, 1200},
		}),
		priorityOnPick: prometheus.NewHistogram(prometheus.HistogramOpts{
			Name:    "execution_priority_on_pick",
			Help:    "Execution priority observed when ExecutionScheduler returns a job candidate to JobScheduler.",
			Buckets: []float64{0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 25, 50, 100},
		}),
		progressRatioOnPick: prometheus.NewHistogram(prometheus.HistogramOpts{
			Name:    "execution_progress_ratio_on_pick",
			Help:    "Execution progress ratio observed when ExecutionScheduler returns a job candidate to JobScheduler.",
			Buckets: []float64{0, 0.01, 0.05, 0.1, 0.25, 0.5, 0.75, 0.9, 0.95, 0.99, 1},
		}),
		progressRatioOnFinish: prometheus.NewHistogram(prometheus.HistogramOpts{
			Name:    "execution_progress_ratio_on_finish",
			Help:    "Execution progress ratio observed when execution finishes.",
			Buckets: []float64{0, 0.01, 0.05, 0.1, 0.25, 0.5, 0.75, 0.9, 0.95, 0.99, 1},
		}),
		schedulerPickTotal: prometheus.NewCounter(prometheus.CounterOpts{
			Name: "execution_scheduler_pick_total",
			Help: "Total number of job candidates returned by ExecutionScheduler to JobScheduler.",
		}),
	}
}

func (m *executionAggregateMetrics) Register(r prometheus.Registerer) error {
	m.finishedTotal.WithLabelValues("ok")
	m.finishedTotal.WithLabelValues("error")
	return errors.Join(
		r.Register(m.startedTotal),
		r.Register(m.finishedTotal),
		r.Register(m.durationSeconds),
		r.Register(m.priorityOnPick),
		r.Register(m.progressRatioOnPick),
		r.Register(m.progressRatioOnFinish),
		r.Register(m.schedulerPickTotal),
	)
}

func (m *executionAggregateMetrics) executionStarted() {
	m.startedTotal.Inc()
}

func (m *executionAggregateMetrics) executionPick(priority, progressRatio float64) {
	m.schedulerPickTotal.Inc()
	m.priorityOnPick.Observe(priority)
	m.progressRatioOnPick.Observe(progressRatio)
}

func (m *executionAggregateMetrics) executionFinished(status string, duration time.Duration, progressRatio float64) {
	m.finishedTotal.WithLabelValues(status).Inc()
	m.durationSeconds.Observe(duration.Seconds())
	m.progressRatioOnFinish.Observe(progressRatio)
}

func newExecutionSchedulerCollector(s *ExecutionScheduler) *executionSchedulerCollector {
	labels := []string{"execution_id"}
	return &executionSchedulerCollector{
		scheduler: s,
		activeExecutionsDesc: prometheus.NewDesc(
			"active_executions",
			"Number of executions currently tracked by ExecutionScheduler.",
			nil, nil,
		),
		executionPriorityDesc: prometheus.NewDesc(
			"execution_priority",
			"Current ExecutionScheduler priority value per active execution.",
			labels, nil,
		),
		executionProgressRatioDesc: prometheus.NewDesc(
			"execution_progress_ratio",
			"Execution progress ratio based on done expected time divided by total expected time.",
			labels, nil,
		),
		executionProgressDesc: prometheus.NewDesc(
			"execution_progress_seconds",
			"Seconds since active execution was scheduled.",
			labels, nil,
		),
		executionTotalExpectedDesc: prometheus.NewDesc(
			"execution_total_expected_seconds",
			"Total expected job runtime for an active execution.",
			labels, nil,
		),
		executionDoneExpectedDesc: prometheus.NewDesc(
			"execution_done_expected_seconds",
			"Expected runtime of jobs already completed in an active execution.",
			labels, nil,
		),
		executionRetriesDesc: prometheus.NewDesc(
			"execution_retry_count",
			"Retry count used by the ExecutionScheduler priority formula.",
			labels, nil,
		),
	}
}

func (c *executionSchedulerCollector) Describe(ch chan<- *prometheus.Desc) {
	ch <- c.activeExecutionsDesc
	ch <- c.executionPriorityDesc
	ch <- c.executionProgressRatioDesc
	ch <- c.executionProgressDesc
	ch <- c.executionTotalExpectedDesc
	ch <- c.executionDoneExpectedDesc
	ch <- c.executionRetriesDesc
}

func (c *executionSchedulerCollector) Collect(ch chan<- prometheus.Metric) {
	executions := c.scheduler.getExecutionsSnapshot()
	now := time.Now()

	ch <- prometheus.MustNewConstMetric(c.activeExecutionsDesc, prometheus.GaugeValue, float64(len(executions)))
	for _, ex := range executions {
		exID := ex.ID.String()
		priority := ex.GetPriority(now)
		progressSeconds := ex.getProgressTime(now) / 1000.0
		totalExpectedSeconds := float64(ex.TotalExpectedTime) / 1000.0
		doneExpectedSeconds := float64(ex.TotalDoneJobsExpectedTime) / 1000.0
		progressRatio := 0.0
		if ex.TotalExpectedTime > 0 {
			progressRatio = float64(ex.TotalDoneJobsExpectedTime) / float64(ex.TotalExpectedTime)
		}
		retryCount := max(0, ex.Tries-1)

		ch <- prometheus.MustNewConstMetric(c.executionPriorityDesc, prometheus.GaugeValue, priority, exID)
		ch <- prometheus.MustNewConstMetric(c.executionProgressRatioDesc, prometheus.GaugeValue, progressRatio, exID)
		ch <- prometheus.MustNewConstMetric(c.executionProgressDesc, prometheus.GaugeValue, progressSeconds, exID)
		ch <- prometheus.MustNewConstMetric(c.executionTotalExpectedDesc, prometheus.GaugeValue, totalExpectedSeconds, exID)
		ch <- prometheus.MustNewConstMetric(c.executionDoneExpectedDesc, prometheus.GaugeValue, doneExpectedSeconds, exID)
		ch <- prometheus.MustNewConstMetric(c.executionRetriesDesc, prometheus.GaugeValue, float64(retryCount), exID)
	}
}

func (s *ExecutionScheduler) getExecutionsSnapshot() []*Execution {
	s.mu.Lock()
	defer s.mu.Unlock()

	executions := make([]*Execution, 0, len(s.executions))
	for _, ex := range s.executions {
		executions = append(executions, ex)
	}
	return executions
}

var _ prometheus.Collector = (*executionSchedulerCollector)(nil)
