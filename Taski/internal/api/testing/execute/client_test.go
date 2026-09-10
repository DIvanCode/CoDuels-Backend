package execute

import (
	"context"
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"testing"
)

func TestExecuteSendsInternalAuth(t *testing.T) {
	const key = "configured-execute-test-key"
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			t.Errorf("method = %s", r.Method)
		}
		if r.URL.Path != "/execute" {
			t.Errorf("path = %s", r.URL.Path)
		}
		if values := r.Header.Values("internal-auth"); len(values) != 1 || values[0] != key {
			t.Error("incorrect internal-auth header")
		}
		var request Request
		if err := json.NewDecoder(r.Body).Decode(&request); err != nil {
			t.Errorf("decode request: %v", err)
		}
		if r.Header.Get("internal-auth") != key {
			w.WriteHeader(http.StatusUnauthorized)
			return
		}
		_, _ = io.WriteString(w, `{"execution_id":"execution-test"}`)
	}))
	defer server.Close()
	client := NewExecuteClient(slog.Default(), server.URL, key)
	id, err := client.Execute(context.Background(), nil, nil)
	if err != nil {
		t.Fatal(err)
	}
	if string(id) != "execution-test" {
		t.Errorf("execution ID = %s", id)
	}
}
