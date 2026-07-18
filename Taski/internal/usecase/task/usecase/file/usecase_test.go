package file

import (
	"context"
	"errors"
	"io"
	"log/slog"
	"strings"
	"testing"

	"taski/internal/domain/task"
	"taski/internal/domain/task/tasks"
)

func TestReadUsesExplicitPublicFileAllowlist(t *testing.T) {
	t.Parallel()

	writeCodeTask := &tasks.WriteCodeTask{
		Details: task.Details{
			Type:      task.WriteCode,
			Statement: "statement.html",
		},
		SourceCode: &task.Code{Path: "starter/main.cpp"},
		Checker:    task.Code{Path: "private/checker.cpp"},
		Solution:   task.Code{Path: "private/solution.cpp"},
		Tests: []task.Test{
			{ID: 1, Input: "tests/01.in", Output: "tests/01.out", Visible: true},
			{ID: 2, Input: "tests/02.in", Output: "tests/02.out", Visible: false},
		},
	}
	predictOutputTask := &tasks.PredictOutputTask{
		Details: task.Details{
			Type:      task.PredictOutput,
			Statement: "statement.html",
		},
		Code:    task.Code{Path: "program/main.cpp"},
		Checker: task.Code{Path: "private/checker.cpp"},
		Test:    task.Test{Input: "tests/input.txt", Output: "tests/answer.txt"},
	}
	findTestTask := &tasks.FindTestTask{
		Details: task.Details{
			Type:      task.FindTest,
			Statement: "statement.html",
		},
		Code:     task.Code{Path: "program/buggy.cpp"},
		Solution: task.Code{Path: "private/solution.cpp"},
		Checker:  task.Code{Path: "private/checker.cpp"},
	}

	tests := []struct {
		name        string
		task        task.Task
		file        string
		wantFile    string
		wantErr     error
		wantGetCall bool
	}{
		{name: "write code statement", task: writeCodeTask, file: "statement.html", wantFile: "statement.html", wantGetCall: true},
		{name: "write code starter", task: writeCodeTask, file: "starter/main.cpp", wantFile: "starter/main.cpp", wantGetCall: true},
		{name: "write code portable separator", task: writeCodeTask, file: `starter\main.cpp`, wantFile: "starter/main.cpp", wantGetCall: true},
		{name: "write code visible input", task: writeCodeTask, file: "tests/01.in", wantFile: "tests/01.in", wantGetCall: true},
		{name: "write code visible output", task: writeCodeTask, file: "tests/01.out", wantFile: "tests/01.out", wantGetCall: true},
		{name: "write code hidden input", task: writeCodeTask, file: "tests/02.in", wantErr: ErrForbidden},
		{name: "write code hidden output", task: writeCodeTask, file: "tests/02.out", wantErr: ErrForbidden},
		{name: "write code reference solution", task: writeCodeTask, file: "private/solution.cpp", wantErr: ErrForbidden},
		{name: "write code checker", task: writeCodeTask, file: "private/checker.cpp", wantErr: ErrForbidden},
		{name: "predict output statement", task: predictOutputTask, file: "statement.html", wantFile: "statement.html", wantGetCall: true},
		{name: "predict output code", task: predictOutputTask, file: "program/main.cpp", wantFile: "program/main.cpp", wantGetCall: true},
		{name: "predict output input", task: predictOutputTask, file: "tests/input.txt", wantFile: "tests/input.txt", wantGetCall: true},
		{name: "predict output answer", task: predictOutputTask, file: "tests/answer.txt", wantErr: ErrForbidden},
		{name: "predict output checker", task: predictOutputTask, file: "private/checker.cpp", wantErr: ErrForbidden},
		{name: "find test statement", task: findTestTask, file: "statement.html", wantFile: "statement.html", wantGetCall: true},
		{name: "find test code", task: findTestTask, file: "program/buggy.cpp", wantFile: "program/buggy.cpp", wantGetCall: true},
		{name: "find test solution", task: findTestTask, file: "private/solution.cpp", wantErr: ErrForbidden},
		{name: "find test checker", task: findTestTask, file: "private/checker.cpp", wantErr: ErrForbidden},
		{name: "parent traversal", task: writeCodeTask, file: "../tests/02.in", wantErr: ErrInvalidPath},
		{name: "embedded traversal", task: writeCodeTask, file: "tests/../private/solution.cpp", wantErr: ErrInvalidPath},
		{name: "absolute path", task: writeCodeTask, file: "/etc/passwd", wantErr: ErrInvalidPath},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			t.Parallel()

			storage := &stubTaskStorage{
				storedTask: tt.task,
				files:      map[string]string{tt.wantFile: "public content"},
			}
			uc := NewUseCase(discardLogger(), storage)
			r, unlock, err := uc.Read(context.Background(), Query{TaskID: testTaskID(t), File: tt.file})

			if !errors.Is(err, tt.wantErr) {
				t.Fatalf("Read() error = %v, want %v", err, tt.wantErr)
			}
			if tt.wantErr != nil {
				if r != nil || unlock != nil {
					t.Fatal("Read() returned a reader or unlock function with an error")
				}
				if storage.getFileCalled {
					t.Fatal("forbidden or invalid path reached file storage")
				}
				return
			}

			defer unlock()
			defer func() { _ = r.Close() }()
			content, readErr := io.ReadAll(r)
			if readErr != nil {
				t.Fatalf("read returned content: %v", readErr)
			}
			if string(content) != "public content" {
				t.Fatalf("Read() content = %q", content)
			}
			if storage.requestedFile != tt.wantFile {
				t.Fatalf("storage requested file = %q, want %q", storage.requestedFile, tt.wantFile)
			}
			if storage.getFileCalled != tt.wantGetCall {
				t.Fatalf("storage GetFile called = %v, want %v", storage.getFileCalled, tt.wantGetCall)
			}
		})
	}
}

func TestReadReturnsClassifiedNotFoundErrors(t *testing.T) {
	t.Parallel()

	taskValue := &tasks.FindTestTask{
		Details: task.Details{Type: task.FindTest, Statement: "statement.html"},
		Code:    task.Code{Path: "buggy.cpp"},
	}

	tests := []struct {
		name    string
		storage *stubTaskStorage
		wantErr error
	}{
		{
			name:    "task missing",
			storage: &stubTaskStorage{getErr: task.ErrNotFound},
			wantErr: task.ErrNotFound,
		},
		{
			name: "allowed file missing",
			storage: &stubTaskStorage{
				storedTask: taskValue,
				getFileErr: task.ErrFileNotFound,
			},
			wantErr: task.ErrFileNotFound,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			t.Parallel()

			uc := NewUseCase(discardLogger(), tt.storage)
			_, _, err := uc.Read(context.Background(), Query{TaskID: testTaskID(t), File: "statement.html"})
			if !errors.Is(err, tt.wantErr) {
				t.Fatalf("Read() error = %v, want %v", err, tt.wantErr)
			}
		})
	}
}

func TestReadDoesNotPanicOnMismatchedTaskType(t *testing.T) {
	t.Parallel()

	storage := &stubTaskStorage{
		storedTask: &tasks.PredictOutputTask{
			Details: task.Details{Type: task.FindTest, Statement: "statement.html"},
			Code:    task.Code{Path: "buggy.cpp"},
		},
	}
	uc := NewUseCase(discardLogger(), storage)

	_, _, err := uc.Read(context.Background(), Query{TaskID: testTaskID(t), File: "statement.html"})
	if err == nil {
		t.Fatal("Read() returned nil error for mismatched task type")
	}
	if errors.Is(err, ErrForbidden) || errors.Is(err, ErrInvalidPath) {
		t.Fatalf("Read() classified corrupt task metadata as a client error: %v", err)
	}
}

type stubTaskStorage struct {
	storedTask    task.Task
	getErr        error
	getFileErr    error
	files         map[string]string
	requestedFile string
	getFileCalled bool
}

func (s *stubTaskStorage) Get(context.Context, task.ID) (task.Task, func(), error) {
	if s.getErr != nil {
		return nil, nil, s.getErr
	}
	return s.storedTask, func() {}, nil
}

func (s *stubTaskStorage) GetFile(_ context.Context, _ task.ID, file string) (io.ReadCloser, func(), error) {
	s.getFileCalled = true
	s.requestedFile = file
	if s.getFileErr != nil {
		return nil, nil, s.getFileErr
	}
	content, ok := s.files[file]
	if !ok {
		return nil, nil, task.ErrFileNotFound
	}
	return io.NopCloser(strings.NewReader(content)), func() {}, nil
}

func discardLogger() *slog.Logger {
	return slog.New(slog.NewTextHandler(io.Discard, nil))
}

func testTaskID(t *testing.T) task.ID {
	t.Helper()

	var id task.ID
	if err := id.FromString(strings.Repeat("a", 40)); err != nil {
		t.Fatalf("create task id: %v", err)
	}
	return id
}
