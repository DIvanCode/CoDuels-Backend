package execute

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"log/slog"
	"net/http"
	"taski/internal/domain/testing"
)

type ExecuteClient struct {
	log      *slog.Logger
	endpoint string
}

func NewExecuteClient(log *slog.Logger, endpoint string) *ExecuteClient {
	return &ExecuteClient{
		log:      log,
		endpoint: endpoint,
	}
}

func (c *ExecuteClient) Execute(ctx context.Context, steps []testing.Step) (executionID testing.ExecutionID, err error) {
	c.log.Info("Execute called", slog.Int("steps_count", len(steps)))

	req := Request{Steps: steps}
	jsonReq, err := json.Marshal(req)
	if err != nil {
		err = fmt.Errorf("failed to marshal execute request: %w", err)
		return
	}
	httpReq, err := http.NewRequestWithContext(
		ctx,
		http.MethodPost,
		c.endpoint+"/execute",
		bytes.NewBuffer(jsonReq))
	if err != nil {
		err = fmt.Errorf("failed to create execute request: %w", err)
		return
	}

	httpClient := http.Client{}
	httpResp, err := httpClient.Do(httpReq)
	if err != nil {
		err = fmt.Errorf("failed to send execute request: %w", err)
		return
	}
	defer func() { _ = httpResp.Body.Close() }()

	if httpResp.StatusCode != http.StatusOK {
		var content []byte
		content, err = io.ReadAll(httpResp.Body)
		if err != nil {
			err = fmt.Errorf("failed to read execute response: %w", err)
			return
		}
		err = fmt.Errorf("execute got response error (status %d): %s", httpResp.StatusCode, string(content))
		return
	}

	var resp Response
	if err = json.NewDecoder(httpResp.Body).Decode(&resp); err != nil {
		err = fmt.Errorf("failed to decode execute response: %w", err)
		return
	}

	return resp.ExecutionID, nil
}
