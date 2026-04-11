package messages

import (
	"exesh/internal/api"
	"exesh/internal/domain/execution"
	"exesh/internal/domain/execution/message/history"
	messagesUC "exesh/internal/usecase/messages"
	"log/slog"
	"net/http"
	"strconv"

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
	r.Get("/executions/{execution_id}/messages", h.HandleGet)
}

func (h *Handler) HandleGet(w http.ResponseWriter, r *http.Request) {
	executionIDStr := chi.URLParam(r, "execution_id")
	if executionIDStr == "" {
		render.Status(r, http.StatusBadRequest)
		render.JSON(w, r, errorResponse("missing execution_id"))
		return
	}

	var executionID execution.ID
	if err := executionID.FromString(executionIDStr); err != nil {
		h.log.Info("failed to parse execution id", slog.String("execution_id", executionIDStr), slog.Any("err", err))
		render.Status(r, http.StatusBadRequest)
		render.JSON(w, r, errorResponse("invalid execution_id"))
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

	msgs, err := h.uc.Get(r.Context(), executionID, startID, count)
	if err != nil {
		h.log.Error("failed to get execution messages", slog.String("execution_id", executionID.String()), slog.Any("err", err))
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
