package runtime

import (
	"context"
	"errors"
	"io"
	"time"
)

type MemoryLimit int64

type TimeLimit time.Duration

const (
	Byte     MemoryLimit = 1
	Kilobyte MemoryLimit = 1000 * Byte
	Megabyte MemoryLimit = 1000 * Kilobyte
)

type Limits struct {
	Memory MemoryLimit
	Time   TimeLimit
}

type File struct {
	InsideLocation  string
	OutsideLocation string
}

type ExecuteParams struct {
	Limits   Limits
	InFiles  []File
	OutFiles []File
	Stdin    io.Reader
	Stdout   io.Writer
	Stderr   io.Writer
}

type LimitError error

var (
	ErrOutOfMemory LimitError = errors.New("out of memory")
	ErrTimeout     LimitError = errors.New("timeout")
)

// Runtime is some place where we can execute some commands with constraints
//
// a Runtime may be shared or isolated, local or remote, generic or specific for some task, and so on
type Runtime interface {
	Execute(ctx context.Context, cmd []string, params ExecuteParams) error
}
