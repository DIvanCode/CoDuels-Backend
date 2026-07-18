package safepath

import (
	"errors"
	"testing"
)

func TestClean(t *testing.T) {
	t.Parallel()

	tests := []struct {
		name string
		path string
		want string
	}{
		{name: "file", path: "statement.html", want: "statement.html"},
		{name: "nested file", path: "tests/01.in", want: "tests/01.in"},
		{name: "portable separators", path: `starter\main.cpp`, want: "starter/main.cpp"},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			t.Parallel()

			got, err := Clean(tt.path)
			if err != nil {
				t.Fatalf("Clean(%q) returned error: %v", tt.path, err)
			}
			if got != tt.want {
				t.Fatalf("Clean(%q) = %q, want %q", tt.path, got, tt.want)
			}
		})
	}
}

func TestCleanRejectsUnsafePaths(t *testing.T) {
	t.Parallel()

	paths := []string{
		"",
		".",
		"..",
		"../secret",
		"tests/../secret",
		"tests/./01.in",
		"tests//01.in",
		"tests/",
		"/etc/passwd",
		`C:\Windows\system.ini`,
		"C:/Windows/system.ini",
		"statement.html\x00secret",
	}

	for _, unsafePath := range paths {
		unsafePath := unsafePath
		t.Run(unsafePath, func(t *testing.T) {
			t.Parallel()

			_, err := Clean(unsafePath)
			if !errors.Is(err, ErrInvalidPath) {
				t.Fatalf("Clean(%q) error = %v, want ErrInvalidPath", unsafePath, err)
			}
		})
	}
}
