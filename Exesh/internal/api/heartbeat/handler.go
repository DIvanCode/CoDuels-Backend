package heartbeat

import (
	"encoding/json"
	"exesh/internal/api"
	"exesh/internal/domain/execution"
	"exesh/internal/usecase/heartbeat"
	"log/slog"
	"net/http"

	"github.com/go-chi/chi/v5"
	"github.com/go-chi/render"
)

type Handler struct {
	log *slog.Logger
	uc  *heartbeat.UseCase
}

func NewHandler(log *slog.Logger, useCase *heartbeat.UseCase) *Handler {
	return &Handler{
		log: log,
		uc:  useCase,
	}
}

func (h *Handler) Register(r chi.Router) {
	r.Post("/heartbeat", h.Handle)
}

func (h *Handler) Handle(w http.ResponseWriter, r *http.Request) {
	var req Request
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		h.log.Info("failed to decode request", slog.Any("err", err))
		render.JSON(w, r, errorResponse("failed to decode request"))
		return
	}

	command := buildCommand(req)
	jobs, err := h.uc.Heartbeat(r.Context(), command)
	if err != nil {
		h.log.Error("failed to process heartbeat",
			slog.Any("command", command),
			slog.Any("err", err))
		render.JSON(w, r, errorResponse("failed to process heartbeat"))
		return
	}

	render.JSON(w, r, okResponse(jobs))
	return
}

func buildCommand(req Request) heartbeat.Command {
	return heartbeat.Command{
		WorkerID:  req.WorkerID,
		DoneJobs:  req.DoneJobs,
		FreeSlots: req.FreeSlots,
	}
}

func okResponse(jobs []execution.Job) Response {
	return Response{
		Response: api.OK(),
		Jobs:     jobs,
	}
}

func errorResponse(msg string) Response {
	return Response{
		Response: api.Error(msg),
	}
}
