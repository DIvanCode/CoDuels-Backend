package random

import (
	"log/slog"
	"net/http"
	"taski/internal/api"
	"taski/internal/domain/task"
	"taski/internal/usecase/task/usecase/random"

	"github.com/go-chi/chi/v5"
	"github.com/go-chi/chi/v5/middleware"
	"github.com/go-chi/render"
)

type Handler struct {
	log *slog.Logger
	uc  *random.UseCase
}

func NewHandler(log *slog.Logger, useCase *random.UseCase) *Handler {
	return &Handler{
		log: log,
		uc:  useCase,
	}
}

func (h *Handler) Register(r chi.Router) {
	r.Get("/task/random", h.Handle)
}

func (h *Handler) Handle(w http.ResponseWriter, r *http.Request) {
	const op = "task.random"

	h.log = h.log.With(
		slog.String("op", op),
		slog.String("request_id", middleware.GetReqID(r.Context())),
	)

	query := random.Query{}
	taskID, err := h.uc.Random(query)
	if err != nil {
		render.Status(r, http.StatusInternalServerError)
		render.JSON(w, r, errorResponse(err.Error()))
		return
	}

	render.Status(r, http.StatusOK)
	render.JSON(w, r, okResponse(taskID))
	return
}

func okResponse(taskID task.ID) RandomTaskResponse {
	return RandomTaskResponse{
		Response: api.OK(),
		TaskID:   taskID,
	}
}

func errorResponse(msg string) RandomTaskResponse {
	return RandomTaskResponse{
		Response: api.Error(msg),
	}
}
