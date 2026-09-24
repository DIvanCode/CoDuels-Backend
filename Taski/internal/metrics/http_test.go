package metrics

import (
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/go-chi/chi/v5"
	"github.com/prometheus/client_golang/prometheus"
)

func TestHTTPMetricsUsesRouteTemplateAndStatus(t *testing.T) {
	metrics := NewHTTPMetrics()
	registry := prometheus.NewRegistry()
	if err := metrics.Register(registry); err != nil {
		t.Fatal(err)
	}
	router := chi.NewRouter()
	router.Use(metrics.Middleware)
	router.Get("/items/{id}", func(w http.ResponseWriter, _ *http.Request) {
		w.WriteHeader(http.StatusServiceUnavailable)
	})
	router.ServeHTTP(httptest.NewRecorder(), httptest.NewRequest(http.MethodGet, "/items/123", nil))

	families, err := registry.Gather()
	if err != nil {
		t.Fatal(err)
	}
	for _, family := range families {
		if family.GetName() != "http_server_request_duration_seconds" {
			continue
		}
		for _, metric := range family.GetMetric() {
			labels := map[string]string{}
			for _, label := range metric.GetLabel() {
				labels[label.GetName()] = label.GetValue()
			}
			if labels["http_route"] == "/items/{id}" &&
				labels["http_response_status_code"] == "503" &&
				labels["http_request_method"] == "GET" &&
				metric.GetHistogram().GetSampleCount() == 1 {
				return
			}
		}
	}
	t.Fatal("expected one 503 sample labelled with the route template")
}
