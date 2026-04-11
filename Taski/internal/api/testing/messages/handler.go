package messages

import (
	"log/slog"
	"net/http"
	"strconv"
	"taski/internal/api"
	"taski/internal/domain/testing"
	"taski/internal/domain/testing/message/history"
	messagesUC "taski/internal/usecase/testing/usecase/messages"

	"github.com/go-chi/chi/v5"
	"github.com/go-chi/render"
)

type Handler struct {
	log *slog.Logger
	uc  *messagesUC.UseCase
}

func NewHandler(log *slog.Logger, uc *messagesUC.UseCase) *Handler {
	return &Handler{
		log: log,
		uc:  uc,
	}
}

func (h *Handler) Register(r chi.Router) {
	r.Get("/solutions/{solution_id}/messages", h.HandleGet)
}

func (h *Handler) HandleGet(w http.ResponseWriter, r *http.Request) {
	solutionID := testing.ExternalSolutionID(chi.URLParam(r, "solution_id"))
	if solutionID == "" {
		render.Status(r, http.StatusBadRequest)
		render.JSON(w, r, errorResponse("missing solution_id"))
		return
	}

	startID, err := parseInt64Query(r, "start_id")
	if err != nil || startID < 1 {
		render.Status(r, http.StatusBadRequest)
		render.JSON(w, r, errorResponse("invalid start_id"))
		return
	}

	count, err := parseIntQuery(r, "count")
	if err != nil || count < 1 {
		render.Status(r, http.StatusBadRequest)
		render.JSON(w, r, errorResponse("invalid count"))
		return
	}

	msgs, err := h.uc.Get(r.Context(), solutionID, startID, count)
	if err != nil {
		h.log.Error("failed to get testing messages", slog.String("solution_id", string(solutionID)), slog.Any("err", err))
		render.Status(r, http.StatusInternalServerError)
		render.JSON(w, r, errorResponse("failed to get messages"))
		return
	}

	render.Status(r, http.StatusOK)
	render.JSON(w, r, okResponse(msgs))
}

func parseInt64Query(r *http.Request, key string) (int64, error) {
	value := r.URL.Query().Get(key)
	if value == "" {
		return 0, strconv.ErrSyntax
	}
	return strconv.ParseInt(value, 10, 64)
}

func parseIntQuery(r *http.Request, key string) (int, error) {
	value := r.URL.Query().Get(key)
	if value == "" {
		return 0, strconv.ErrSyntax
	}
	return strconv.Atoi(value)
}

func okResponse(msgs []history.Message) Response {
	return Response{
		Response: api.OK(),
		Messages: msgs,
	}
}

func errorResponse(msg string) Response {
	return Response{Response: api.Error(msg)}
}
