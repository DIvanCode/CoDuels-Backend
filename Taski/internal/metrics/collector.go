package metrics

import (
	"context"
	"errors"
	"fmt"
	"log/slog"
	"strconv"
	"taski/internal/config"
	"taski/internal/domain/task"
	"taski/internal/domain/testing"
	"time"

	"github.com/prometheus/client_golang/prometheus"
)

type (
	Collector struct {
		log *slog.Logger
		cfg config.MetricsCollectorConfig

		taskStorage taskStorage

		unitOfWork      unitOfWork
		solutionStorage solutionStorage

		tasksGauge     *prometheus.GaugeVec
		solutionsGauge *prometheus.GaugeVec
	}

	taskStorage interface {
		GetList() ([]task.Task, error)
	}

	unitOfWork interface {
		Do(context.Context, func(ctx context.Context) error) error
	}

	solutionStorage interface {
		GetAll(context.Context) ([]testing.Solution, error)
	}

	taskLabels string

	taskLabelsKey struct {
		Id    task.ID
		Name  string
		Level task.Level
	}

	solutionLabels string

	solutionLabelsKey struct {
		TaskId   task.ID
		Language task.Language
	}
)

const (
	taskIdLabel    taskLabels = "id"
	taskNameLabel  taskLabels = "name"
	taskLevelLabel taskLabels = "level"

	solutionTaskIdLabel   solutionLabels = "task_id"
	solutionLanguageLabel solutionLabels = "language"
)

func (key taskLabelsKey) values() []string {
	return []string{
		key.Id.String(),
		key.Name,
		strconv.Itoa(int(key.Level)),
	}
}

func (key solutionLabelsKey) values() []string {
	return []string{
		key.TaskId.String(),
		string(key.Language),
	}
}

func NewMetricsCollector(
	log *slog.Logger,
	cfg config.MetricsCollectorConfig,
	taskStorage taskStorage,
	unitOfWork unitOfWork,
	solutionStorage solutionStorage,
) *Collector {
	return &Collector{
		log: log,
		cfg: cfg,

		taskStorage: taskStorage,

		unitOfWork:      unitOfWork,
		solutionStorage: solutionStorage,

		tasksGauge: prometheus.NewGaugeVec(
			prometheus.GaugeOpts{
				Name: "tasks_total",
				Help: "Count of tasks in system",
			},
			[]string{
				string(taskIdLabel),
				string(taskNameLabel),
				string(taskLevelLabel),
			}),

		solutionsGauge: prometheus.NewGaugeVec(
			prometheus.GaugeOpts{
				Name: "solutions_process_time_avg",
				Help: "Average solution process time",
			},
			[]string{
				string(solutionTaskIdLabel),
				string(solutionLanguageLabel),
			}),
	}
}

func (c *Collector) RegisterMetrics(r prometheus.Registerer) error {
	return errors.Join(
		r.Register(c.tasksGauge),
		r.Register(c.solutionsGauge),
	)
}

func (c *Collector) Start(ctx context.Context) {
	go c.run(ctx)
}

func (c *Collector) run(ctx context.Context) {
	for {
		timer := time.NewTicker(c.cfg.CollectInterval)

		select {
		case <-ctx.Done():
			return
		case <-timer.C:
			break
		}

		if err := c.collectTasks(); err != nil {
			c.log.Error("failed to collect tasks", slog.Any("err", err))
		}

		if err := c.collectSolutions(ctx); err != nil {
			c.log.Error("failed to collect solutions", slog.Any("err", err))
		}
	}
}

func (c *Collector) collectTasks() error {
	tasks, err := c.taskStorage.GetList()
	if err != nil {
		return fmt.Errorf("failed to get all tasks: %w", err)
	}

	groups := make(map[taskLabelsKey]int)
	for _, t := range tasks {
		labels := taskLabelsKey{
			Id:    t.GetID(),
			Name:  t.GetTitle(),
			Level: t.GetLevel(),
		}

		val, ok := groups[labels]
		if !ok {
			val = 0
		}
		val++

		groups[labels] = val
	}

	for key, val := range groups {
		c.tasksGauge.WithLabelValues(key.values()...).Set(float64(val))
	}

	return nil
}

func (c *Collector) collectSolutions(ctx context.Context) error {
	err := c.unitOfWork.Do(ctx, func(ctx context.Context) error {
		solutions, err := c.solutionStorage.GetAll(ctx)
		if err != nil {
			return fmt.Errorf("failed to get all solutions: %w", err)
		}

		groupsTotalDuration := make(map[solutionLabelsKey]time.Duration)
		groupsCountSubmissions := make(map[solutionLabelsKey]int)
		for _, s := range solutions {
			if s.FinishedAt == nil {
				continue
			}

			labels := solutionLabelsKey{
				TaskId:   s.TaskID,
				Language: s.Lang,
			}

			dur, ok := groupsTotalDuration[labels]
			if !ok {
				dur = time.Duration(0)
			}
			dur += s.ProcessTime()

			cnt, ok := groupsCountSubmissions[labels]
			if !ok {
				cnt = 0
			}
			cnt += 1

			groupsTotalDuration[labels] = dur
			groupsCountSubmissions[labels] = cnt
		}

		for key, dur := range groupsTotalDuration {
			cnt := groupsCountSubmissions[key]
			c.solutionsGauge.WithLabelValues(key.values()...).Set(dur.Seconds() / float64(cnt))
		}

		return nil
	})

	if err != nil {
		return fmt.Errorf("failed to collect solutions: %w", err)
	}

	return nil
}
