package scheduler

import (
	"sync"
	"time"

	"github.com/prometheus/client_golang/prometheus"
)

const (
	defaultJobMetricsRetention       = 5 * time.Minute
	defaultJobMetricsMaxFinishedJobs = 10000
)

type (
	jobSchedulerMetrics struct {
		mu sync.Mutex

		scheduler       *JobScheduler
		retention       time.Duration
		maxFinishedJobs int
		finishedJobs    []jobMetricRecord

		promisedJobsDesc       *prometheus.Desc
		startedJobsDesc        *prometheus.Desc
		promisedJobMemoryDesc  *prometheus.Desc
		promisedJobTimeDesc    *prometheus.Desc
		promisedJobStartDesc   *prometheus.Desc
		jobMemoryDesc          *prometheus.Desc
		jobMemoryStartDesc     *prometheus.Desc
		jobMemoryEndDesc       *prometheus.Desc
		jobStartDesc           *prometheus.Desc
		jobFinishDesc          *prometheus.Desc
		jobExpectedFinishDesc  *prometheus.Desc
		jobExpectedSecondsDesc *prometheus.Desc
	}

	jobMetricRecord struct {
		jobID                   string
		executionID             string
		workerID                string
		jobType                 string
		status                  string
		expectedMemory          int
		memoryOffset            int
		startedAt               time.Time
		finishedAt              time.Time
		expectedFinishedAt      time.Time
		expectedDurationSeconds float64
	}
)

func newJobSchedulerMetrics(retention time.Duration, maxFinishedJobs int) *jobSchedulerMetrics {
	if retention <= 0 {
		retention = defaultJobMetricsRetention
	}
	if maxFinishedJobs <= 0 {
		maxFinishedJobs = defaultJobMetricsMaxFinishedJobs
	}

	jobLabels := []string{"job_id", "execution_id", "worker_id", "job_type", "status"}
	promisedLabels := []string{"job_id", "execution_id", "worker_id", "job_type"}
	return &jobSchedulerMetrics{
		retention:       retention,
		maxFinishedJobs: maxFinishedJobs,
		finishedJobs:    make([]jobMetricRecord, 0),

		promisedJobsDesc: prometheus.NewDesc(
			"job_scheduler_promised_jobs",
			"Number of jobs currently promised by JobScheduler.",
			nil, nil,
		),
		startedJobsDesc: prometheus.NewDesc(
			"job_scheduler_started_jobs",
			"Number of jobs currently started and tracked by JobScheduler.",
			nil, nil,
		),
		promisedJobMemoryDesc: prometheus.NewDesc(
			"job_scheduler_promised_job_expected_memory_mb",
			"Expected memory in MiB for currently promised jobs.",
			promisedLabels, nil,
		),
		promisedJobTimeDesc: prometheus.NewDesc(
			"job_scheduler_promised_job_expected_duration_seconds",
			"Expected duration in seconds for currently promised jobs.",
			promisedLabels, nil,
		),
		promisedJobStartDesc: prometheus.NewDesc(
			"job_scheduler_promised_job_start_timestamp_seconds",
			"Promised start timestamp for currently promised jobs.",
			promisedLabels, nil,
		),
		jobMemoryDesc: prometheus.NewDesc(
			"job_scheduler_job_rectangle_memory_mb",
			"Expected memory in MiB for running and recently finished jobs.",
			jobLabels, nil,
		),
		jobMemoryStartDesc: prometheus.NewDesc(
			"job_scheduler_job_rectangle_memory_start_mb",
			"Lower memory coordinate in MiB for the job rectangle on its worker.",
			jobLabels, nil,
		),
		jobMemoryEndDesc: prometheus.NewDesc(
			"job_scheduler_job_rectangle_memory_end_mb",
			"Upper memory coordinate in MiB for the job rectangle on its worker.",
			jobLabels, nil,
		),
		jobStartDesc: prometheus.NewDesc(
			"job_scheduler_job_rectangle_start_timestamp_seconds",
			"Actual start timestamp for running and recently finished jobs.",
			jobLabels, nil,
		),
		jobFinishDesc: prometheus.NewDesc(
			"job_scheduler_job_rectangle_finish_timestamp_seconds",
			"Actual finish timestamp for recently finished jobs; 0 while running.",
			jobLabels, nil,
		),
		jobExpectedFinishDesc: prometheus.NewDesc(
			"job_scheduler_job_rectangle_expected_finish_timestamp_seconds",
			"Expected finish timestamp based on actual start and expected duration.",
			jobLabels, nil,
		),
		jobExpectedSecondsDesc: prometheus.NewDesc(
			"job_scheduler_job_rectangle_expected_duration_seconds",
			"Expected job duration in seconds.",
			jobLabels, nil,
		),
	}
}

func (m *jobSchedulerMetrics) Describe(ch chan<- *prometheus.Desc) {
	ch <- m.promisedJobsDesc
	ch <- m.startedJobsDesc
	ch <- m.promisedJobMemoryDesc
	ch <- m.promisedJobTimeDesc
	ch <- m.promisedJobStartDesc
	ch <- m.jobMemoryDesc
	ch <- m.jobMemoryStartDesc
	ch <- m.jobMemoryEndDesc
	ch <- m.jobStartDesc
	ch <- m.jobFinishDesc
	ch <- m.jobExpectedFinishDesc
	ch <- m.jobExpectedSecondsDesc
}

func (m *jobSchedulerMetrics) Collect(ch chan<- prometheus.Metric) {
	now := time.Now()
	var promised []promisedJob
	var started []jobMetricRecord
	if m.scheduler != nil {
		m.scheduler.mu.Lock()
		promised = make([]promisedJob, len(m.scheduler.promisedJobs))
		copy(promised, m.scheduler.promisedJobs)
		started = make([]jobMetricRecord, 0, len(m.scheduler.startedJobs))
		for _, startedJb := range m.scheduler.startedJobs {
			started = append(started, buildJobMetricRecord(
				startedJb.workerID,
				startedJb.Job,
				"running",
				startedJb.startedAt,
				time.Time{},
				startedJb.memoryOffset,
			))
		}
		m.scheduler.mu.Unlock()
	}

	ch <- prometheus.MustNewConstMetric(m.promisedJobsDesc, prometheus.GaugeValue, float64(len(promised)))
	ch <- prometheus.MustNewConstMetric(m.startedJobsDesc, prometheus.GaugeValue, float64(len(started)))

	for _, jb := range promised {
		collectPromisedJob(ch, m, jb)
	}
	for _, record := range started {
		collectJobRectangle(ch, m, record)
	}
	for _, record := range m.finishedSnapshot(now) {
		collectJobRectangle(ch, m, record)
	}
}

func (m *jobSchedulerMetrics) observeFinished(record jobMetricRecord) {
	m.mu.Lock()
	defer m.mu.Unlock()

	m.finishedJobs = append(m.finishedJobs, record)
	m.pruneLocked(time.Now())
}

func (m *jobSchedulerMetrics) finishedSnapshot(now time.Time) []jobMetricRecord {
	m.mu.Lock()
	defer m.mu.Unlock()

	m.pruneLocked(now)
	res := make([]jobMetricRecord, len(m.finishedJobs))
	copy(res, m.finishedJobs)
	return res
}

func (m *jobSchedulerMetrics) pruneLocked(now time.Time) {
	from := 0
	for from < len(m.finishedJobs) && now.Sub(m.finishedJobs[from].finishedAt) > m.retention {
		from++
	}
	if from > 0 {
		copy(m.finishedJobs, m.finishedJobs[from:])
		m.finishedJobs = m.finishedJobs[:len(m.finishedJobs)-from]
	}
	if overflow := len(m.finishedJobs) - m.maxFinishedJobs; overflow > 0 {
		copy(m.finishedJobs, m.finishedJobs[overflow:])
		m.finishedJobs = m.finishedJobs[:len(m.finishedJobs)-overflow]
	}
}

func (s *JobScheduler) RegisterMetrics(r prometheus.Registerer) error {
	return r.Register(s.metrics)
}

func buildJobMetricRecord(workerID string, jb *Job, status string, startedAt time.Time, finishedAt time.Time, memoryOffset int) jobMetricRecord {
	expectedDurationSeconds := float64(jb.GetExpectedTime()) / 1000.0
	jobID := jb.GetID()
	return jobMetricRecord{
		jobID:                   jobID.String(),
		executionID:             jb.ExecutionID.String(),
		workerID:                workerID,
		jobType:                 string(jb.GetType()),
		status:                  status,
		expectedMemory:          jb.GetExpectedMemory(),
		memoryOffset:            memoryOffset,
		startedAt:               startedAt,
		finishedAt:              finishedAt,
		expectedFinishedAt:      startedAt.Add(time.Duration(jb.GetExpectedTime()) * time.Millisecond),
		expectedDurationSeconds: expectedDurationSeconds,
	}
}

func collectJobRectangle(ch chan<- prometheus.Metric, m *jobSchedulerMetrics, record jobMetricRecord) {
	labels := []string{record.jobID, record.executionID, record.workerID, record.jobType, record.status}
	finish := 0.0
	if !record.finishedAt.IsZero() {
		finish = float64(record.finishedAt.UnixMilli()) / 1000.0
	}

	ch <- prometheus.MustNewConstMetric(m.jobMemoryDesc, prometheus.GaugeValue, float64(record.expectedMemory), labels...)
	ch <- prometheus.MustNewConstMetric(m.jobMemoryStartDesc, prometheus.GaugeValue, float64(record.memoryOffset), labels...)
	ch <- prometheus.MustNewConstMetric(m.jobMemoryEndDesc, prometheus.GaugeValue, float64(record.memoryOffset+record.expectedMemory), labels...)
	ch <- prometheus.MustNewConstMetric(m.jobStartDesc, prometheus.GaugeValue, float64(record.startedAt.UnixMilli())/1000.0, labels...)
	ch <- prometheus.MustNewConstMetric(m.jobFinishDesc, prometheus.GaugeValue, finish, labels...)
	ch <- prometheus.MustNewConstMetric(m.jobExpectedFinishDesc, prometheus.GaugeValue, float64(record.expectedFinishedAt.UnixMilli())/1000.0, labels...)
	ch <- prometheus.MustNewConstMetric(m.jobExpectedSecondsDesc, prometheus.GaugeValue, record.expectedDurationSeconds, labels...)
}

func collectPromisedJob(ch chan<- prometheus.Metric, m *jobSchedulerMetrics, jb promisedJob) {
	jobID := jb.GetID()
	labels := []string{jobID.String(), jb.ExecutionID.String(), jb.PromisedWorkerID, string(jb.GetType())}
	ch <- prometheus.MustNewConstMetric(m.promisedJobMemoryDesc, prometheus.GaugeValue, float64(jb.GetExpectedMemory()), labels...)
	ch <- prometheus.MustNewConstMetric(m.promisedJobTimeDesc, prometheus.GaugeValue, float64(jb.GetExpectedTime())/1000.0, labels...)
	ch <- prometheus.MustNewConstMetric(m.promisedJobStartDesc, prometheus.GaugeValue, float64(jb.PromisedStartAt.UnixMilli())/1000.0, labels...)
}

var _ prometheus.Collector = (*jobSchedulerMetrics)(nil)
