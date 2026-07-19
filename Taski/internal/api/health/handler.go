package health

import (
	"net/http"

	"github.com/go-chi/chi/v5"
	"github.com/go-chi/render"
)

type Response struct {
	Status string `json:"status"`
}

func Register(r chi.Router) {
	r.Get("/health", Handle)
}

func Handle(w http.ResponseWriter, r *http.Request) {
	render.Status(r, http.StatusOK)
	render.JSON(w, r, Response{Status: "ok"})
}
