package file

import (
	"errors"
	"fmt"
	"io"
	"log/slog"
	"mime"
	"net/http"
	"net/url"
	"path/filepath"
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
	r.Get("/task/{id}/*", h.Handle)
}

func (h *Handler) Handle(w http.ResponseWriter, r *http.Request) {
	const op = "task.file"

	log := h.log.With(
		slog.String("op", op),
		slog.String("request_id", middleware.GetReqID(r.Context())),
	)

	id := chi.URLParam(r, "id")
	if id == "" {
		log.Info("empty id")
		render.Status(r, http.StatusBadRequest)
		render.JSON(w, r, errorResponse("empty task id"))
		return
	}

	var taskID task.ID
	if err := taskID.FromString(id); err != nil {
		log.Info("invalid id", slog.String("id", id), slog.Any("error", err))
		render.Status(r, http.StatusBadRequest)
		render.JSON(w, r, errorResponse("invalid task id"))
		return
	}

	path, err := url.PathUnescape(chi.URLParam(r, "*"))
	if err != nil {
		log.Info("invalid escaped path", slog.Any("error", err))
		render.Status(r, http.StatusBadRequest)
		render.JSON(w, r, errorResponse("invalid file path"))
		return
	}
	if path == "" {
		log.Info("empty path")
		render.Status(r, http.StatusBadRequest)
		render.JSON(w, r, errorResponse("empty file path"))
		return
	}

	query := file.Query{TaskID: taskID, File: path}
	rc, unlock, err := h.uc.Read(r.Context(), query)
	if err != nil {
		status, message := publicError(err)
		if status >= http.StatusInternalServerError {
			log.Error("failed to read task file", slog.String("file", path), slog.Any("error", err))
		} else {
			log.Info("task file request rejected", slog.String("file", path), slog.Any("error", err))
		}
		render.Status(r, status)
		render.JSON(w, r, errorResponse(message))
		return
	}
	defer unlock()
	defer func() { _ = rc.Close() }()

	ext := filepath.Ext(path)
	contentType := mime.TypeByExtension(ext)
	if contentType == "" {
		contentType = "application/octet-stream"
	}

	w.Header().Set("Content-Type", contentType)
	w.Header().Set("Content-Disposition", fmt.Sprintf("inline; filename=%q", filepath.Base(path)))
	if _, err = io.Copy(w, rc); err != nil {
		log.Error("failed to stream task file", slog.String("file", path), slog.Any("error", err))
	}

	return
}

func errorResponse(msg string) api.Response {
	return api.Error(msg)
}

func publicError(err error) (int, string) {
	switch {
	case errors.Is(err, file.ErrInvalidPath):
		return http.StatusBadRequest, "invalid file path"
	case errors.Is(err, file.ErrForbidden):
		return http.StatusForbidden, "task file access forbidden"
	case errors.Is(err, task.ErrNotFound):
		return http.StatusNotFound, "task not found"
	case errors.Is(err, task.ErrFileNotFound):
		return http.StatusNotFound, "task file not found"
	default:
		return http.StatusInternalServerError, "internal server error"
	}
}
