package topics

import (
	"log/slog"
	"net/http"
	"taski/internal/api"
	"taski/internal/usecase/task/usecase/topics"

	"github.com/go-chi/chi/v5"
	"github.com/go-chi/chi/v5/middleware"
	"github.com/go-chi/render"
)

type Handler struct {
	log *slog.Logger
	uc  *topics.UseCase
}

func NewHandler(log *slog.Logger, useCase *topics.UseCase) *Handler {
	return &Handler{
		log: log,
		uc:  useCase,
	}
}

func (h *Handler) Register(r chi.Router) {
	r.Get("/task/topics", h.Handle)
}

func (h *Handler) Handle(w http.ResponseWriter, r *http.Request) {
	const op = "task.topics"

	h.log = h.log.With(
		slog.String("op", op),
		slog.String("request_id", middleware.GetReqID(r.Context())),
	)

	query := topics.Query{}
	taskTopics, err := h.uc.Get(query)
	if err != nil {
		render.Status(r, http.StatusInternalServerError)
		render.JSON(w, r, errorResponse(err.Error()))
		return
	}

	render.Status(r, http.StatusOK)
	render.JSON(w, r, okResponse(taskTopics))
	return
}

func okResponse(taskTopics []string) TaskTopicsListResponse {
	return TaskTopicsListResponse{
		Response: api.OK(),
		Topics:   taskTopics,
	}
}

func errorResponse(msg string) TaskTopicsListResponse {
	return TaskTopicsListResponse{
		Response: api.Error(msg),
	}
}
