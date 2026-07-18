package taskiexeshe2e

import (
	"bytes"
	"context"
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http"
	"os"
	"sort"
	"strings"
	"testing"
	"time"
)

const (
	aPlusBTaskID = "7d971f50363cf0aebbd87d971f50363cf0aebbd8"
	accepted     = "Accepted"
	finish       = "finish"
)

var aPlusBSolution = strings.TrimSpace(`
#include <iostream>

int main() {
    long long a, b;
    std::cin >> a >> b;
    std::cout << a + b << '\n';
    return 0;
}
`) + "\n"

type apiResponse struct {
	Status string `json:"status"`
	Error  string `json:"error"`
}

type messagesResponse struct {
	apiResponse
	Messages []storedMessage `json:"messages"`
}

type storedMessage struct {
	MessageID int64           `json:"message_id"`
	Message   json.RawMessage `json:"message"`
}

type testingMessage struct {
	SolutionID string `json:"solution_id"`
	Type       string `json:"type"`
	Status     string `json:"status"`
	Verdict    string `json:"verdict"`
	Error      string `json:"error"`
	Message    string `json:"message"`
}

func TestABSolutionAccepted(t *testing.T) {
	if os.Getenv("CODUELS_E2E") != "1" {
		t.Skip("run with ./run.sh to start the Taski/Exesh stack")
	}

	taskiURL := strings.TrimRight(envOrDefault("TASKI_URL", "http://taski:5252"), "/")
	coordinatorURL := strings.TrimRight(envOrDefault("COORDINATOR_URL", "http://coordinator:5253"), "/")
	client := &http.Client{Timeout: 10 * time.Second}

	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Minute)
	defer cancel()

	if err := waitForHTTPServer(ctx, client, coordinatorURL); err != nil {
		t.Fatalf("coordinator did not become ready: %v", err)
	}
	if err := waitForStatus(ctx, client, taskiURL+"/task/"+aPlusBTaskID, http.StatusOK); err != nil {
		t.Fatalf("Taski did not expose the A+B task: %v", err)
	}

	solutionID, err := randomSolutionID()
	if err != nil {
		t.Fatalf("create random solution ID: %v", err)
	}
	t.Logf("solution_id=%s", solutionID)

	if err = submitSolution(ctx, client, taskiURL, solutionID); err != nil {
		t.Fatalf("submit A+B solution: %v", err)
	}

	last, err := waitForAcceptedFinish(ctx, client, taskiURL, solutionID)
	if err != nil {
		t.Fatal(err)
	}

	// Re-read the history after the system has settled to prove that Accepted
	// remains the last Taski message rather than an intermediate observation.
	select {
	case <-ctx.Done():
		t.Fatalf("wait for stable final status: %v", ctx.Err())
	case <-time.After(time.Second):
	}

	stableLast, stableMessage, err := fetchLastMessage(ctx, client, taskiURL, solutionID)
	if err != nil {
		t.Fatalf("re-read final Taski message: %v", err)
	}
	if stableLast.MessageID != last.MessageID || stableMessage.Type != finish || stableMessage.Verdict != accepted {
		t.Fatalf(
			"Taski final message changed: first=%s, stable=%s",
			formatMessage(last, testingMessage{Type: finish, Verdict: accepted}),
			formatMessage(stableLast, stableMessage),
		)
	}
}

func submitSolution(ctx context.Context, client *http.Client, taskiURL, solutionID string) error {
	payload := struct {
		SolutionID string `json:"solution_id"`
		TaskID     string `json:"task_id"`
		Solution   string `json:"solution"`
		Language   string `json:"language"`
	}{
		SolutionID: solutionID,
		TaskID:     aPlusBTaskID,
		Solution:   aPlusBSolution,
		Language:   "Cpp",
	}

	body, err := json.Marshal(payload)
	if err != nil {
		return fmt.Errorf("marshal request: %w", err)
	}

	req, err := http.NewRequestWithContext(ctx, http.MethodPost, taskiURL+"/test", bytes.NewReader(body))
	if err != nil {
		return fmt.Errorf("create request: %w", err)
	}
	req.Header.Set("Content-Type", "application/json")

	resp, err := client.Do(req)
	if err != nil {
		return fmt.Errorf("send request: %w", err)
	}
	defer resp.Body.Close()

	content, err := io.ReadAll(resp.Body)
	if err != nil {
		return fmt.Errorf("read response: %w", err)
	}
	if resp.StatusCode != http.StatusOK {
		return fmt.Errorf("Taski returned status %d: %s", resp.StatusCode, strings.TrimSpace(string(content)))
	}

	var result apiResponse
	if err = json.Unmarshal(content, &result); err != nil {
		return fmt.Errorf("decode response: %w", err)
	}
	if result.Status != "OK" {
		return fmt.Errorf("Taski rejected submission: status=%q error=%q", result.Status, result.Error)
	}

	return nil
}

func waitForAcceptedFinish(
	ctx context.Context,
	client *http.Client,
	taskiURL string,
	solutionID string,
) (storedMessage, error) {
	ticker := time.NewTicker(250 * time.Millisecond)
	defer ticker.Stop()

	var last storedMessage
	var lastMessage testingMessage
	for {
		current, message, err := fetchLastMessage(ctx, client, taskiURL, solutionID)
		if err == nil && current.MessageID > 0 {
			last = current
			lastMessage = message
			if message.Type == finish {
				if message.SolutionID != solutionID {
					return storedMessage{}, fmt.Errorf("final message has solution_id=%q, want %q", message.SolutionID, solutionID)
				}
				if message.Verdict != accepted {
					return storedMessage{}, fmt.Errorf("A+B was not accepted: %s", formatMessage(current, message))
				}
				return current, nil
			}
		}

		select {
		case <-ctx.Done():
			if last.MessageID == 0 {
				return storedMessage{}, fmt.Errorf("timed out waiting for Taski messages: %w", ctx.Err())
			}
			return storedMessage{}, fmt.Errorf(
				"timed out waiting for Accepted final message; last=%s: %w",
				formatMessage(last, lastMessage),
				ctx.Err(),
			)
		case <-ticker.C:
		}
	}
}

func fetchLastMessage(
	ctx context.Context,
	client *http.Client,
	taskiURL string,
	solutionID string,
) (storedMessage, testingMessage, error) {
	url := fmt.Sprintf("%s/solutions/%s/messages?start_id=1&count=1000", taskiURL, solutionID)
	req, err := http.NewRequestWithContext(ctx, http.MethodGet, url, nil)
	if err != nil {
		return storedMessage{}, testingMessage{}, fmt.Errorf("create messages request: %w", err)
	}

	resp, err := client.Do(req)
	if err != nil {
		return storedMessage{}, testingMessage{}, fmt.Errorf("request messages: %w", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		content, readErr := io.ReadAll(resp.Body)
		if readErr != nil {
			return storedMessage{}, testingMessage{}, fmt.Errorf("read messages error response: %w", readErr)
		}
		return storedMessage{}, testingMessage{}, fmt.Errorf(
			"messages endpoint returned status %d: %s",
			resp.StatusCode,
			strings.TrimSpace(string(content)),
		)
	}

	var result messagesResponse
	if err = json.NewDecoder(resp.Body).Decode(&result); err != nil {
		return storedMessage{}, testingMessage{}, fmt.Errorf("decode messages response: %w", err)
	}
	if result.Status != "OK" {
		return storedMessage{}, testingMessage{}, fmt.Errorf("messages endpoint failed: %s", result.Error)
	}
	if len(result.Messages) == 0 {
		return storedMessage{}, testingMessage{}, nil
	}

	sort.Slice(result.Messages, func(i, j int) bool {
		return result.Messages[i].MessageID < result.Messages[j].MessageID
	})
	for i := 1; i < len(result.Messages); i++ {
		if result.Messages[i-1].MessageID == result.Messages[i].MessageID {
			return storedMessage{}, testingMessage{}, fmt.Errorf("duplicate Taski message_id=%d", result.Messages[i].MessageID)
		}
	}

	last := result.Messages[len(result.Messages)-1]
	var message testingMessage
	if err = json.Unmarshal(last.Message, &message); err != nil {
		return storedMessage{}, testingMessage{}, fmt.Errorf("decode Taski message %d: %w", last.MessageID, err)
	}

	return last, message, nil
}

func waitForHTTPServer(ctx context.Context, client *http.Client, url string) error {
	return waitFor(ctx, func() error {
		req, err := http.NewRequestWithContext(ctx, http.MethodGet, url, nil)
		if err != nil {
			return err
		}
		resp, err := client.Do(req)
		if err != nil {
			return err
		}
		return resp.Body.Close()
	})
}

func waitForStatus(ctx context.Context, client *http.Client, url string, want int) error {
	return waitFor(ctx, func() error {
		req, err := http.NewRequestWithContext(ctx, http.MethodGet, url, nil)
		if err != nil {
			return err
		}
		resp, err := client.Do(req)
		if err != nil {
			return err
		}
		defer resp.Body.Close()
		if resp.StatusCode != want {
			return fmt.Errorf("got HTTP %d, want %d", resp.StatusCode, want)
		}
		return nil
	})
}

func waitFor(ctx context.Context, check func() error) error {
	ticker := time.NewTicker(250 * time.Millisecond)
	defer ticker.Stop()

	var lastErr error
	for {
		if err := check(); err == nil {
			return nil
		} else {
			lastErr = err
		}

		select {
		case <-ctx.Done():
			return errors.Join(ctx.Err(), lastErr)
		case <-ticker.C:
		}
	}
}

func randomSolutionID() (string, error) {
	random := make([]byte, 16)
	if _, err := rand.Read(random); err != nil {
		return "", err
	}
	return "e2e-" + hex.EncodeToString(random), nil
}

func envOrDefault(key, fallback string) string {
	if value := os.Getenv(key); value != "" {
		return value
	}
	return fallback
}

func formatMessage(stored storedMessage, message testingMessage) string {
	return fmt.Sprintf(
		"message_id=%d solution_id=%q type=%q status=%q verdict=%q error=%q message=%q",
		stored.MessageID,
		message.SolutionID,
		message.Type,
		message.Status,
		message.Verdict,
		message.Error,
		message.Message,
	)
}
