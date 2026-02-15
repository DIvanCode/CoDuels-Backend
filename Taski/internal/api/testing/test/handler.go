package test

import (
	"encoding/json"
	"log/slog"
	"net/http"
	"taski/internal/api"
	"taski/internal/usecase/testing/usecase/test"

	"github.com/go-chi/chi/v5"
	"github.com/go-chi/chi/v5/middleware"
	"github.com/go-chi/render"
)

type Handler struct {
	log *slog.Logger
	uc  *test.UseCase
}

func NewHandler(log *slog.Logger, useCase *test.UseCase) *Handler {
	return &Handler{
		log: log,
		uc:  useCase,
	}
}

func (h *Handler) Register(r chi.Router) {
	r.Post("/test", h.Handle)
}

func (h *Handler) Handle(w http.ResponseWriter, r *http.Request) {
	const op = "test"

	h.log = h.log.With(
		slog.String("op", op),
		slog.String("request_id", middleware.GetReqID(r.Context())),
	)

	req := Request{}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		h.log.Info("failed to unmarshal request", slog.Any("error", err))
		render.Status(r, http.StatusBadRequest)
		render.JSON(w, r, errorResponse("invalid request"))
		return
	}

	command := test.Command{
		ExternalSolutionID: req.ExternalSolutionID,
		TaskID:             req.TaskID,
		Solution:           req.Solution,
		Lang:               req.Lang,
	}
	if err := h.uc.Test(r.Context(), command); err != nil {
		h.log.Error("failed to test task", slog.Any("err", err))
		render.Status(r, http.StatusInternalServerError)
		render.JSON(w, r, errorResponse("failed to test task"))
		return
	}

	render.JSON(w, r, okResponse())
	return
}

func okResponse() api.Response {
	return api.OK()
}

func errorResponse(msg string) api.Response {
	return api.Error(msg)
}
