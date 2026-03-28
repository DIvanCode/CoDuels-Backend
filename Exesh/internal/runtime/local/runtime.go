package local

import (
	"context"
	"errors"
	"exesh/internal/runtime"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"time"
)

type Runtime struct {
	rootDir string
}

type FuncOpt func(r *Runtime) error

func New() *Runtime {
	return &Runtime{}
}

func (rt *Runtime) InitRuntime() error {
	if rt.rootDir != "" {
		return nil
	}

	rootDir, err := os.MkdirTemp("", "exesh-local-runtime-*")
	if err != nil {
		return fmt.Errorf("create local runtime dir: %w", err)
	}

	rt.rootDir = rootDir
	return nil
}

func (rt *Runtime) CopyToRuntime(src, dst string) error {
	return copyFile(src, rt.runtimePath(dst))
}

func (rt *Runtime) CopyFromRuntime(src, dst string) error {
	return copyFile(rt.runtimePath(src), dst)
}

func (rt *Runtime) RunCommand(ctx context.Context, cmd []string, params runtime.RunParams) error {
	if len(cmd) == 0 {
		return fmt.Errorf("empty command")
	}
	if rt.rootDir == "" {
		return fmt.Errorf("runtime is not initialized")
	}

	ctxExec := ctx
	var cancel context.CancelFunc
	if params.Limits.Time != 0 {
		ctxExec, cancel = context.WithTimeout(ctx, time.Duration(params.Limits.Time))
		defer cancel()
	}

	execCmd := exec.CommandContext(ctxExec, cmd[0], cmd[1:]...)
	execCmd.Dir = rt.rootDir
	execCmd.Stderr = params.Stderr

	if params.StdinFile != "" {
		f, err := os.Open(rt.runtimePath(params.StdinFile))
		if err != nil {
			return fmt.Errorf("open stdin file: %w", err)
		}
		defer func() { _ = f.Close() }()
		execCmd.Stdin = f
	}

	if params.StdoutFile != "" {
		stdoutPath := rt.runtimePath(params.StdoutFile)
		if err := os.MkdirAll(filepath.Dir(stdoutPath), 0o755); err != nil {
			return fmt.Errorf("create stdout dir: %w", err)
		}
		f, err := os.OpenFile(stdoutPath, os.O_CREATE|os.O_TRUNC|os.O_WRONLY, 0o666)
		if err != nil {
			return fmt.Errorf("open stdout file: %w", err)
		}
		defer func() { _ = f.Close() }()
		execCmd.Stdout = f
	}

	if err := execCmd.Run(); err != nil {
		if errorsIsTimeout(ctxExec) {
			return runtime.ErrTimeout
		}
		return err
	}

	return nil
}

func (rt *Runtime) StopRuntime() error {
	if rt.rootDir == "" {
		return nil
	}
	err := os.RemoveAll(rt.rootDir)
	rt.rootDir = ""
	return err
}

func (rt *Runtime) runtimePath(path string) string {
	return filepath.Join(rt.rootDir, path)
}

func errorsIsTimeout(ctx context.Context) bool {
	if ctx == nil {
		return false
	}
	return errors.Is(ctx.Err(), context.DeadlineExceeded)
}

func copyFile(src, dst string) error {
	in, err := os.Open(src)
	if err != nil {
		return err
	}
	defer func() { _ = in.Close() }()

	st, err := in.Stat()
	if err != nil {
		return err
	}

	if err := os.MkdirAll(filepath.Dir(dst), 0o755); err != nil {
		return err
	}

	mode := os.FileMode(0o644)
	if st.Mode().Perm() != 0 {
		mode = st.Mode().Perm()
	}

	out, err := os.OpenFile(dst, os.O_CREATE|os.O_TRUNC|os.O_WRONLY, mode)
	if err != nil {
		return err
	}
	defer func() { _ = out.Close() }()

	if _, err := io.Copy(out, in); err != nil {
		return err
	}

	return out.Close()
}
