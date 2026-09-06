package consumer

import (
	"context"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"taski/internal/config"
	domain "taski/internal/domain/testing"
	"testing"
)

func TestFetchMessagesSendsInternalAuth(t *testing.T) {
	for _, status := range []int{http.StatusOK, http.StatusUnauthorized} {
		t.Run(http.StatusText(status), func(t *testing.T) {
			const key = "configured-poll-test-key"
			server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				if r.Method != http.MethodGet {
					t.Errorf("method = %s", r.Method)
				}
				if r.URL.Path != "/executions/execution-test/messages" {
					t.Errorf("path = %s", r.URL.Path)
				}
				if r.URL.Query().Get("start_id") != "7" {
					t.Error("incorrect start_id")
				}
				if r.URL.Query().Get("count") != "10" {
					t.Error("incorrect count")
				}
				if values := r.Header.Values("internal-auth"); len(values) != 1 || values[0] != key {
					t.Error("incorrect internal-auth header")
				}
				w.WriteHeader(status)
				_, _ = io.WriteString(w, `{"status":"OK","messages":[]}`)
			}))
			defer server.Close()
			poller := NewEventPoller(slog.Default(), config.EventConsumerConfig{RestEndpoint: server.URL}, key, nil, nil, nil)
			_, err := poller.fetchMessages(context.Background(), domain.Solution{ExecutionID: "execution-test"}, 7, 10)
			if status == http.StatusOK {
				if err != nil {
					t.Fatal(err)
				}
			} else {
				if err == nil || !strings.Contains(err.Error(), "401") {
					t.Errorf("expected HTTP 401 error, got %v", err)
				}
			}
		})
	}
}
