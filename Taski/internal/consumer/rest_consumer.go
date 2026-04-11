package consumer

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"log/slog"
	"net/http"
	"sort"
	"strings"
	"taski/internal/config"
	"taski/internal/domain/testing"
	"taski/internal/domain/testing/event/events"
	"taski/internal/usecase/testing/usecase/update"
	"time"
)

type EventPoller struct {
	log *slog.Logger
	cfg config.EventConsumerConfig

	httpClient      http.Client
	unitOfWork      unitOfWork
	solutionStorage restSolutionStorage
	usecase         *update.UseCase
}

type (
	unitOfWork interface {
		Do(context.Context, func(ctx context.Context) error) error
	}

	restSolutionStorage interface {
		GetInProgress(context.Context) ([]testing.Solution, error)
	}

	executionMessagesResponse struct {
		Status   string                   `json:"status"`
		Messages []executionMessageRecord `json:"messages"`
	}

	executionMessageRecord struct {
		MessageID int64           `json:"message_id"`
		Message   json.RawMessage `json:"message"`
	}
)

func NewEventPoller(
	log *slog.Logger,
	cfg config.EventConsumerConfig,
	unitOfWork unitOfWork,
	solutionStorage restSolutionStorage,
	usecase *update.UseCase,
) *EventPoller {
	return &EventPoller{
		log: log,
		cfg: cfg,

		httpClient:      http.Client{},
		unitOfWork:      unitOfWork,
		solutionStorage: solutionStorage,
		usecase:         usecase,
	}
}

func (c *EventPoller) Start(ctx context.Context) {
	go c.runPoller(ctx)
}

func (c *EventPoller) runPoller(ctx context.Context) {
	pollInterval := c.cfg.PollInterval
	if pollInterval <= 0 {
		pollInterval = 250 * time.Millisecond
	}

	ticker := time.NewTicker(pollInterval)
	defer ticker.Stop()

	c.log.Info(
		"rest consumer started",
		slog.String("endpoint", c.cfg.RestEndpoint),
		slog.Duration("poll_interval", pollInterval),
		slog.Int("messages_count", c.messagesCount()),
	)

	for {
		select {
		case <-ticker.C:
			if err := c.pollAll(ctx); err != nil {
				c.log.Error("failed to poll execution messages", slog.Any("error", err))
			}
		case <-ctx.Done():
			return
		}
	}
}

func (c *EventPoller) pollAll(ctx context.Context) error {
	var solutions []testing.Solution
	if err := c.unitOfWork.Do(ctx, func(ctx context.Context) error {
		var err error
		solutions, err = c.solutionStorage.GetInProgress(ctx)
		if err != nil {
			return fmt.Errorf("failed to get in-progress solutions: %w", err)
		}
		return nil
	}); err != nil {
		return err
	}

	for _, sol := range solutions {
		if err := c.pollSolution(ctx, sol); err != nil {
			c.log.Error(
				"failed to poll solution messages",
				slog.String("execution_id", string(sol.ExecutionID)),
				slog.Any("error", err),
			)
		}
	}

	return nil
}

func (c *EventPoller) pollSolution(ctx context.Context, sol testing.Solution) error {
	count := c.messagesCount()
	startID := sol.HandledEventsCount + 1

	for {
		resp, err := c.fetchMessages(ctx, sol, startID, count)
		if err != nil {
			return err
		}
		if len(resp.Messages) == 0 {
			return nil
		}

		sort.Slice(resp.Messages, func(i, j int) bool {
			return resp.Messages[i].MessageID < resp.Messages[j].MessageID
		})

		for _, record := range resp.Messages {
			var evt events.Event
			if err = json.Unmarshal(record.Message, &evt); err != nil {
				return fmt.Errorf("failed to unmarshal message event: %w", err)
			}
			command := update.Command{
				Event:     evt,
				MessageID: &record.MessageID,
			}
			if err = c.usecase.Update(ctx, command); err != nil {
				return fmt.Errorf("failed to process event by message id %d: %w", record.MessageID, err)
			}
			startID = record.MessageID + 1
		}

		if len(resp.Messages) < count {
			return nil
		}
	}
}

func (c *EventPoller) fetchMessages(
	ctx context.Context,
	sol testing.Solution,
	startID int64,
	count int,
) (executionMessagesResponse, error) {
	url := fmt.Sprintf(
		"%s/executions/%s/messages?start_id=%d&count=%d",
		strings.TrimRight(c.cfg.RestEndpoint, "/"),
		sol.ExecutionID,
		startID,
		count,
	)

	httpReq, err := http.NewRequestWithContext(ctx, http.MethodGet, url, nil)
	if err != nil {
		return executionMessagesResponse{}, fmt.Errorf("failed to create request: %w", err)
	}

	httpResp, err := c.httpClient.Do(httpReq)
	if err != nil {
		return executionMessagesResponse{}, fmt.Errorf("failed to send request: %w", err)
	}
	defer func() { _ = httpResp.Body.Close() }()

	if httpResp.StatusCode != http.StatusOK {
		content, readErr := io.ReadAll(httpResp.Body)
		if readErr != nil {
			return executionMessagesResponse{}, fmt.Errorf("failed to read response body for non-200: %w", readErr)
		}
		return executionMessagesResponse{}, fmt.Errorf("got response status %d: %s", httpResp.StatusCode, string(content))
	}

	var resp executionMessagesResponse
	if err = json.NewDecoder(httpResp.Body).Decode(&resp); err != nil {
		return executionMessagesResponse{}, fmt.Errorf("failed to decode response: %w", err)
	}

	return resp, nil
}

func (c *EventPoller) messagesCount() int {
	if c.cfg.MessagesCount > 0 {
		return c.cfg.MessagesCount
	}
	return 100
}

func (c *EventPoller) Close() error {
	return nil
}
