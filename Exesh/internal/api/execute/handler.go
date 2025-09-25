package execute

import (
	"encoding/json"
	"exesh/internal/api"
	"exesh/internal/usecase/execute"
	"log/slog"
	"net/http"

	"github.com/go-chi/chi/v5"
	"github.com/go-chi/render"
)

type Handler struct {
	log *slog.Logger
	uc  *execute.UseCase
}

func NewHandler(log *slog.Logger, uc *execute.UseCase) *Handler {
	return &Handler{
		log: log,
		uc:  uc,
	}
}

func (h *Handler) Register(r chi.Router) {
	r.Post("/execute", h.Handle)
}

func (h *Handler) Handle(w http.ResponseWriter, r *http.Request) {
	var req Request
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		h.log.Info("failed to decode request", slog.Any("err", err))
		render.Status(r, http.StatusBadRequest)
		render.JSON(w, r, errorResponse("failed to decode request"))
		return
	}

	command := execute.Command{Steps: req.Steps}
	result, err := h.uc.Execute(r.Context(), command)
	if err != nil {
		h.log.Error("failed to execute", slog.Any("err", err))
		render.Status(r, http.StatusInternalServerError)
		render.JSON(w, r, errorResponse("failed to process execute"))
		return
	}

	render.Status(r, http.StatusOK)
	render.JSON(w, r, okResponse(result))
	return
}

func okResponse(result execute.Result) Response {
	return Response{
		Response:    api.OK(),
		ExecutionID: &result.ExecutionID,
	}
}

func errorResponse(msg string) Response {
	return Response{
		Response: api.Error(msg),
	}
}
