package get

import (
	"log/slog"
	"net/http"
	"taski/internal/api"
	"taski/internal/domain/task"
	"taski/internal/usecase/task/dto"
	"taski/internal/usecase/task/usecase/get"

	"github.com/go-chi/chi/v5"
	"github.com/go-chi/chi/v5/middleware"
	"github.com/go-chi/render"
)

type Handler struct {
	log *slog.Logger
	uc  *get.UseCase
}

func NewHandler(log *slog.Logger, useCase *get.UseCase) *Handler {
	return &Handler{
		log: log,
		uc:  useCase,
	}
}

func (h *Handler) Register(r chi.Router) {
	r.Get("/task/{id:[a-z0-9]{40}}", h.Handle)
}

func (h *Handler) Handle(w http.ResponseWriter, r *http.Request) {
	const op = "task.get"

	h.log = h.log.With(
		slog.String("op", op),
		slog.String("request_id", middleware.GetReqID(r.Context())),
	)

	id := chi.URLParam(r, "id")
	if id == "" {
		h.log.Info("empty id")
		render.Status(r, http.StatusBadRequest)
		render.JSON(w, r, errorResponse("empty id"))
		return
	}

	var taskID task.ID
	if err := taskID.FromString(id); err != nil {
		h.log.Info("invalid id", slog.String("id", id), slog.Any("error", err))
		render.Status(r, http.StatusBadRequest)
		render.JSON(w, r, errorResponse("invalid id"))
		return
	}

	query := get.Query{TaskID: taskID}
	taskDto, err := h.uc.Get(r.Context(), query)
	if err != nil {
		render.Status(r, http.StatusInternalServerError)
		render.JSON(w, r, errorResponse(err.Error()))
		return
	}

	render.Status(r, http.StatusOK)
	render.JSON(w, r, okResponse(taskDto))
	return
}

func okResponse(task dto.TaskDto) GetTaskResponse {
	return GetTaskResponse{
		Response: api.OK(),
		Task:     task,
	}
}

func errorResponse(msg string) GetTaskResponse {
	return GetTaskResponse{
		Response: api.Error(msg),
	}
}
