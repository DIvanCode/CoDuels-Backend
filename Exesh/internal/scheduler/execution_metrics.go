package scheduler

import (
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
