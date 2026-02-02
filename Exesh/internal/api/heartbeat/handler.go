package heartbeat

import (
	"encoding/json"
	"exesh/internal/api"
	"exesh/internal/domain/execution/job/jobs"
	"exesh/internal/domain/execution/source/sources"
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
	jbs, srcs, err := h.uc.Heartbeat(r.Context(), command)
	if err != nil {
		h.log.Error("failed to process heartbeat",
			slog.Any("command", command),
			slog.Any("err", err))
		render.JSON(w, r, errorResponse("failed to process heartbeat"))
		return
	}

	render.JSON(w, r, okResponse(jbs, srcs))
	return
}

func buildCommand(req Request) heartbeat.Command {
	return heartbeat.Command{
		WorkerID:  req.WorkerID,
		DoneJobs:  req.DoneJobs,
		FreeSlots: req.FreeSlots,
	}
}

func okResponse(jbs []jobs.Job, srcs []sources.Source) Response {
	return Response{
		Response: api.OK(),
		Jobs:     jbs,
		Sources:  srcs,
	}
}

func errorResponse(msg string) Response {
	return Response{
		Response: api.Error(msg),
	}
}
