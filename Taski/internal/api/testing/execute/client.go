package execute

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"log/slog"
	"net/http"
	"taski/internal/domain/testing/execution"
	"taski/internal/domain/testing/source/sources"
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

func (c *ExecuteClient) Execute(
	ctx context.Context,
	stages execution.Stages,
	sources sources.Sources,
) (executionID execution.ID, err error) {
	req := Request{Stages: stages, Sources: sources}
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
