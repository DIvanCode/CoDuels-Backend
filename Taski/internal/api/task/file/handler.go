package file

import (
	"fmt"
	"io"
	"log/slog"
	"mime"
	"net/http"
	"path/filepath"
	"strings"
	"taski/internal/api"
	"taski/internal/domain/task"
	"taski/internal/usecase/task/usecase/file"

	"github.com/go-chi/chi/v5"
	"github.com/go-chi/chi/v5/middleware"
	"github.com/go-chi/render"
)

type Handler struct {
	log *slog.Logger
	uc  *file.UseCase
}

func NewHandler(log *slog.Logger, useCase *file.UseCase) *Handler {
	return &Handler{
		log: log,
		uc:  useCase,
	}
}

func (h *Handler) Register(r chi.Router) {
	r.Get("/task/{id:[a-z0-9]{40}}/*", h.Handle)
}

func (h *Handler) Handle(w http.ResponseWriter, r *http.Request) {
	const op = "task.file"

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

	path := strings.TrimPrefix(r.URL.Path, "/task/"+id+"/")
	if path == "" {
		h.log.Info("empty path")
		render.Status(r, http.StatusBadRequest)
		render.JSON(w, r, errorResponse("empty path"))
		return
	}

	query := file.Query{TaskID: taskID, File: path}
	rc, unlock, err := h.uc.Read(query)
	if err != nil {
		h.log.Error("failed to read file", slog.Any("file", path), slog.Any("err", err))
		render.Status(r, http.StatusInternalServerError)
		render.JSON(w, r, errorResponse(err.Error()))
		return
	}
	defer unlock()

	ext := filepath.Ext(path)
	contentType := mime.TypeByExtension(ext)
	if contentType == "" {
		contentType = "application/octet-stream"
	}

	w.Header().Set("Content-Type", contentType)
	w.Header().Set("Content-Disposition", fmt.Sprintf("inline; filename=%q", filepath.Base(path)))
	if _, err = io.Copy(w, rc); err != nil {
		render.JSON(w, r, errorResponse(err.Error()))
	}

	return
}

func errorResponse(msg string) api.Response {
	return api.Error(msg)
}
