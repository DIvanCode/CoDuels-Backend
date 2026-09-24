package metrics

import (
	"net/http"
	"strconv"
	"time"

	"github.com/go-chi/chi/v5"
	"github.com/go-chi/chi/v5/middleware"
	"github.com/prometheus/client_golang/prometheus"
)

type HTTPMetrics struct {
	duration *prometheus.HistogramVec
}

func NewHTTPMetrics() *HTTPMetrics {
	return &HTTPMetrics{duration: prometheus.NewHistogramVec(prometheus.HistogramOpts{
		Name:    "http_server_request_duration_seconds",
		Help:    "Duration of application HTTP requests",
		Buckets: prometheus.DefBuckets,
	}, []string{"http_request_method", "http_route", "http_response_status_code"})}
}

func (m *HTTPMetrics) Register(r prometheus.Registerer) error {
	return r.Register(m.duration)
}

func (m *HTTPMetrics) Middleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		start := time.Now()
		wrapped := middleware.NewWrapResponseWriter(w, r.ProtoMajor)
		completed := false
		defer func() {
			status := wrapped.Status()
			if !completed {
				status = http.StatusInternalServerError
			} else if status == 0 {
				status = http.StatusOK
			}
			route := chi.RouteContext(r.Context()).RoutePattern()
			if route == "" {
				route = "unmatched"
			}
			m.duration.WithLabelValues(boundedMethod(r.Method), route, strconv.Itoa(status)).Observe(time.Since(start).Seconds())
		}()
		next.ServeHTTP(wrapped, r)
		completed = true
	})
}

func boundedMethod(method string) string {
	switch method {
	case http.MethodGet, http.MethodPost, http.MethodPut, http.MethodPatch, http.MethodDelete, http.MethodOptions, http.MethodHead:
		return method
	default:
		return "OTHER"
	}
}
