package heartbeat

import (
	"bytes"
	"context"
	"encoding/json"
	"exesh/internal/api"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/result/results"
	"exesh/internal/domain/execution/source/sources"
	"fmt"
	"io"
	"net/http"
)

type Client struct {
	endpoint string
}

func NewHeartbeatClient(endpoint string) *Client {
	return &Client{
		endpoint: endpoint,
	}
}

func (c *Client) Heartbeat(
	ctx context.Context,
	workerID string,
	doneJobs []results.Result,
	freeSlots int,
) ([]jobs.Job, []sources.Source, error) {
	req := Request{workerID, doneJobs, freeSlots}
	jsonReq, err := json.Marshal(req)
	if err != nil {
		return nil, nil, err
	}
	httpReq, err := http.NewRequestWithContext(
		ctx,
		http.MethodPost,
		c.endpoint+"/heartbeat",
		bytes.NewBuffer(jsonReq))
	if err != nil {
		return nil, nil, fmt.Errorf("failed to create heartheat request: %w", err)
	}

	httpClient := http.Client{}
	httpResp, err := httpClient.Do(httpReq)
	if err != nil {
		return nil, nil, fmt.Errorf("failed to send heartheat request: %w", err)
	}
	defer func() { _ = httpResp.Body.Close() }()

	if httpResp.StatusCode != http.StatusOK {
		content, err := io.ReadAll(httpResp.Body)
		if err != nil {
			return nil, nil, fmt.Errorf("failed to read heartheat response: %w", err)
		}
		return nil, nil, fmt.Errorf("heartbeat got response error (status %d): %s", httpResp.StatusCode, string(content))
	}

	var resp Response
	if err = json.NewDecoder(httpResp.Body).Decode(&resp); err != nil {
		return nil, nil, fmt.Errorf("failed to decode heartheat response: %w", err)
	}
	if resp.Status != api.StatusOK {
		return nil, nil, fmt.Errorf("heartbeat got response error: %s", resp.Error)
	}

	return resp.Jobs, resp.Sources, nil
}
