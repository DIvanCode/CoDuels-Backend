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

type RunParams struct {
	Limits     Limits    // memory and time limits
	StdinFile  string    // runtime file that is stdin for command
	StdoutFile string    // runtime file that is stdout for command
	Stderr     io.Writer // stderr should be written to this writer
}

type Usage struct {
	ElapsedTime int
	UsedMemory  int
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
	Init(context.Context) error
	CopyToRuntime(context.Context, string, string) error
	CopyFromRuntime(context.Context, string, string) error
	RunCommand(context.Context, []string, RunParams) (*Usage, error)
	Stop(context.Context) error
}
