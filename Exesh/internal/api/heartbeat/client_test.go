package heartbeat

import (
	"context"
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
)

func TestHeartbeatSendsInternalAuth(t *testing.T) {
	for _, status := range []int{http.StatusOK, http.StatusUnauthorized} {
		t.Run(http.StatusText(status), func(t *testing.T) {
			const key = "configured-heartbeat-test-key"
			server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				if r.Method != http.MethodPost || r.URL.Path != "/heartbeat" {
					t.Errorf("unexpected request: %s %s", r.Method, r.URL.Path)
				}
				if values := r.Header.Values("internal-auth"); len(values) != 1 || values[0] != key {
					t.Error("incorrect internal-auth header")
				}
				var request Request
				if err := json.NewDecoder(r.Body).Decode(&request); err != nil {
					t.Errorf("decode request: %v", err)
				}
				if request.WorkerID != "worker-test" || request.TotalSlots != 4 || request.TotalMemory != 1024 || request.FreeSlots != 2 || request.AvailableMemory != 512 {
					t.Errorf("unexpected heartbeat payload: %+v", request)
				}
				w.WriteHeader(status)
				_, _ = io.WriteString(w, `{"status":"OK","jobs":[],"sources":[]}`)
			}))
			defer server.Close()
			client := NewHeartbeatClient(server.URL, key)
			_, _, err := client.Heartbeat(context.Background(), "worker-test", nil, 4, 1024, 2, 512)
			if status == http.StatusOK {
				if err != nil {
					t.Fatal(err)
				}
			} else if err == nil || !strings.Contains(err.Error(), "401") {
				t.Errorf("expected HTTP 401 error, got %v", err)
			}
		})
	}
}
