package api

import (
	"net/http"
	"net/http/httptest"
	"testing"
)

func TestRequireInternalAuth(t *testing.T) {
	tests := []struct {
		name          string
		expectedKey   string
		headers       []string
		expectedCode  int
		expectedCalls int
	}{
		{name: "valid", expectedKey: "test-key", headers: []string{"test-key"}, expectedCode: http.StatusNoContent, expectedCalls: 1},
		{name: "missing", expectedKey: "test-key", expectedCode: http.StatusUnauthorized},
		{name: "empty", expectedKey: "test-key", headers: []string{""}, expectedCode: http.StatusUnauthorized},
		{name: "wrong", expectedKey: "test-key", headers: []string{"wrong-key"}, expectedCode: http.StatusUnauthorized},
		{name: "duplicate", expectedKey: "test-key", headers: []string{"test-key", "wrong-key"}, expectedCode: http.StatusUnauthorized},
		{name: "unconfigured", headers: []string{"test-key"}, expectedCode: http.StatusUnauthorized},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			calls := 0
			next := http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) {
				calls++
				w.WriteHeader(http.StatusNoContent)
			})
			handler := RequireInternalAuth(tc.expectedKey)(next)
			req := httptest.NewRequest(http.MethodGet, "/", nil)
			for _, key := range tc.headers {
				req.Header.Add(internalAuthHeader, key)
			}
			response := httptest.NewRecorder()

			handler.ServeHTTP(response, req)

			if response.Code != tc.expectedCode {
				t.Fatalf("got HTTP %d, want %d", response.Code, tc.expectedCode)
			}
			if calls != tc.expectedCalls {
				t.Fatalf("next called %d times, want %d", calls, tc.expectedCalls)
			}
		})
	}
}
