package list

import (
	"log/slog"
	"net/http"
	"taski/internal/api"
	"taski/internal/usecase/task/dto"
	"taski/internal/usecase/task/usecase/list"

	"github.com/go-chi/chi/v5"
	"github.com/go-chi/chi/v5/middleware"
	"github.com/go-chi/render"
)

type Handler struct {
	log *slog.Logger
	uc  *list.UseCase
}

func NewHandler(log *slog.Logger, useCase *list.UseCase) *Handler {
	return &Handler{
		log: log,
		uc:  useCase,
	}
}

func (h *Handler) Register(r chi.Router) {
	r.Get("/task/list", h.Handle)
}

func (h *Handler) Handle(w http.ResponseWriter, r *http.Request) {
	const op = "task.list"

	h.log = h.log.With(
		slog.String("op", op),
		slog.String("request_id", middleware.GetReqID(r.Context())),
	)

	query := list.Query{}
	tasks, err := h.uc.Get(r.Context(), query)
	if err != nil {
		render.Status(r, http.StatusInternalServerError)
		render.JSON(w, r, errorResponse(err.Error()))
		return
	}

	render.Status(r, http.StatusOK)
	render.JSON(w, r, okResponse(tasks))
	return
}

func okResponse(tasks []dto.TaskDto) TaskListResponse {
	return TaskListResponse{
		Response: api.OK(),
		Tasks:    tasks,
	}
}

func errorResponse(msg string) TaskListResponse {
	return TaskListResponse{
		Response: api.Error(msg),
	}
}
