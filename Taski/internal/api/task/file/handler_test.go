package file

import (
	"context"
	"encoding/json"
	"errors"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"taski/internal/api"
	"taski/internal/domain/task"
	"taski/internal/domain/task/tasks"
	fileusecase "taski/internal/usecase/task/usecase/file"

	"github.com/go-chi/chi/v5"
)

func TestHandlerReturnsStableFileResponses(t *testing.T) {
	t.Parallel()

	const validID = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
	publicTask := &tasks.WriteCodeTask{
		Details:  task.Details{Type: task.WriteCode, Statement: "statement.html"},
		Checker:  task.Code{Path: "checker.cpp"},
		Solution: task.Code{Path: "solution.cpp"},
		Tests: []task.Test{
			{ID: 1, Input: "tests/01.in", Output: "tests/01.out", Visible: true},
			{ID: 2, Input: "tests/02.in", Output: "tests/02.out", Visible: false},
		},
	}
	predictOutputTask := &tasks.PredictOutputTask{
		Details: task.Details{Type: task.PredictOutput, Statement: "statement.html"},
		Code:    task.Code{Path: "program/main.cpp"},
		Checker: task.Code{Path: "private/checker.cpp"},
		Test:    task.Test{Input: "tests/input.txt", Output: "tests/answer.txt"},
	}
	findTestTask := &tasks.FindTestTask{
		Details:  task.Details{Type: task.FindTest, Statement: "statement.html"},
		Code:     task.Code{Path: "program/buggy.cpp"},
		Solution: task.Code{Path: "private/solution.cpp"},
		Checker:  task.Code{Path: "private/checker.cpp"},
	}

	tests := []struct {
		name         string
		url          string
		storage      *handlerTaskStorage
		wantStatus   int
		wantBody     string
		wantAPIError string
	}{
		{
			name: "statement",
			url:  "/task/" + validID + "/statement.html",
			storage: &handlerTaskStorage{
				storedTask: publicTask,
				files:      map[string]string{"statement.html": "statement"},
			},
			wantStatus: http.StatusOK,
			wantBody:   "statement",
		},
		{
			name: "query cannot select hidden file",
			url:  "/task/" + validID + "/statement.html?file=tests%2F02.in",
			storage: &handlerTaskStorage{
				storedTask: publicTask,
				files:      map[string]string{"statement.html": "statement"},
			},
			wantStatus: http.StatusOK,
			wantBody:   "statement",
		},
		{
			name: "predict output input",
			url:  "/task/" + validID + "/tests%2Finput.txt",
			storage: &handlerTaskStorage{
				storedTask: predictOutputTask,
				files:      map[string]string{"tests/input.txt": "input"},
			},
			wantStatus: http.StatusOK,
			wantBody:   "input",
		},
		{
			name:         "predict output answer is hidden",
			url:          "/task/" + validID + "/tests%2Fanswer.txt",
			storage:      &handlerTaskStorage{storedTask: predictOutputTask},
			wantStatus:   http.StatusForbidden,
			wantAPIError: "task file access forbidden",
		},
		{
			name: "find test code",
			url:  "/task/" + validID + "/program%2Fbuggy.cpp",
			storage: &handlerTaskStorage{
				storedTask: findTestTask,
				files:      map[string]string{"program/buggy.cpp": "buggy code"},
			},
			wantStatus: http.StatusOK,
			wantBody:   "buggy code",
		},
		{
			name:         "find test solution is hidden",
			url:          "/task/" + validID + "/private%2Fsolution.cpp",
			storage:      &handlerTaskStorage{storedTask: findTestTask},
			wantStatus:   http.StatusForbidden,
			wantAPIError: "task file access forbidden",
		},
		{
			name:         "invalid task id",
			url:          "/task/not-a-task-id/statement.html",
			storage:      &handlerTaskStorage{},
			wantStatus:   http.StatusBadRequest,
			wantAPIError: "invalid task id",
		},
		{
			name:         "invalid task id character",
			url:          "/task/gggggggggggggggggggggggggggggggggggggggg/statement.html",
			storage:      &handlerTaskStorage{},
			wantStatus:   http.StatusBadRequest,
			wantAPIError: "invalid task id",
		},
		{
			name:         "empty path",
			url:          "/task/" + validID + "/",
			storage:      &handlerTaskStorage{},
			wantStatus:   http.StatusBadRequest,
			wantAPIError: "empty file path",
		},
		{
			name:         "encoded traversal",
			url:          "/task/" + validID + "/%2e%2e/secret",
			storage:      &handlerTaskStorage{storedTask: publicTask},
			wantStatus:   http.StatusBadRequest,
			wantAPIError: "invalid file path",
		},
		{
			name:         "hidden test",
			url:          "/task/" + validID + "/tests/02.in",
			storage:      &handlerTaskStorage{storedTask: publicTask},
			wantStatus:   http.StatusForbidden,
			wantAPIError: "task file access forbidden",
		},
		{
			name:         "task not found",
			url:          "/task/" + validID + "/statement.html",
			storage:      &handlerTaskStorage{getErr: task.ErrNotFound},
			wantStatus:   http.StatusNotFound,
			wantAPIError: "task not found",
		},
		{
			name: "allowed file not found",
			url:  "/task/" + validID + "/statement.html",
			storage: &handlerTaskStorage{
				storedTask: publicTask,
				getFileErr: task.ErrFileNotFound,
			},
			wantStatus:   http.StatusNotFound,
			wantAPIError: "task file not found",
		},
		{
			name: "internal error is not exposed",
			url:  "/task/" + validID + "/statement.html",
			storage: &handlerTaskStorage{
				storedTask: publicTask,
				getFileErr: errors.New("storage credentials leaked"),
			},
			wantStatus:   http.StatusInternalServerError,
			wantAPIError: "internal server error",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			t.Parallel()

			mux := chi.NewRouter()
			uc := fileusecase.NewUseCase(handlerDiscardLogger(), tt.storage)
			NewHandler(handlerDiscardLogger(), uc).Register(mux)

			recorder := httptest.NewRecorder()
			request := httptest.NewRequest(http.MethodGet, tt.url, nil)
			mux.ServeHTTP(recorder, request)

			if recorder.Code != tt.wantStatus {
				t.Fatalf("status = %d, want %d; body = %q", recorder.Code, tt.wantStatus, recorder.Body.String())
			}
			if tt.wantAPIError == "" {
				if recorder.Body.String() != tt.wantBody {
					t.Fatalf("body = %q, want %q", recorder.Body.String(), tt.wantBody)
				}
				return
			}

			var response api.Response
			if err := json.Unmarshal(recorder.Body.Bytes(), &response); err != nil {
				t.Fatalf("decode error response %q: %v", recorder.Body.String(), err)
			}
			if response.Status != api.StatusError || response.Error != tt.wantAPIError {
				t.Fatalf("response = %+v, want status %q and error %q", response, api.StatusError, tt.wantAPIError)
			}
		})
	}
}

type handlerTaskStorage struct {
	storedTask task.Task
	getErr     error
	getFileErr error
	files      map[string]string
}

func (s *handlerTaskStorage) Get(context.Context, task.ID) (task.Task, func(), error) {
	if s.getErr != nil {
		return nil, nil, s.getErr
	}
	return s.storedTask, func() {}, nil
}

func (s *handlerTaskStorage) GetFile(_ context.Context, _ task.ID, file string) (io.ReadCloser, func(), error) {
	if s.getFileErr != nil {
		return nil, nil, s.getFileErr
	}
	content, ok := s.files[file]
	if !ok {
		return nil, nil, task.ErrFileNotFound
	}
	return io.NopCloser(strings.NewReader(content)), func() {}, nil
}

func handlerDiscardLogger() *slog.Logger {
	return slog.New(slog.NewTextHandler(io.Discard, nil))
}
