package topics

import (
	"log/slog"
	"taski/internal/config"
)

type (
	Query struct{}

	UseCase struct {
		log    *slog.Logger
		topics config.TaskTopicsList
	}
)

func NewUseCase(log *slog.Logger, topics config.TaskTopicsList) *UseCase {
	return &UseCase{
		log:    log,
		topics: topics,
	}
}

func (uc *UseCase) Get(_ Query) ([]string, error) {
	return uc.topics, nil
}
