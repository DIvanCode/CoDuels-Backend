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
	"strconv"
	"sync"
	"sync/atomic"
	"time"
)

type Runtime struct {
	nextID uint64

	mu      sync.Mutex
	workDir map[runtime.ID]string
}

type FuncOpt func(r *Runtime) error

func New() *Runtime {
	return &Runtime{
		workDir: make(map[runtime.ID]string),
	}
}

func (rt *Runtime) Init(_ context.Context) (runtime.ID, error) {
	idNum := atomic.AddUint64(&rt.nextID, 1)
	runtimeID := runtime.ID(strconv.FormatUint(idNum, 10))

	workDir, err := os.MkdirTemp("/tmp", "*")
	if err != nil {
		return "", fmt.Errorf("create local runtime dir: %w", err)
	}

	rt.mu.Lock()
	rt.workDir[runtimeID] = workDir
	rt.mu.Unlock()

	return runtimeID, nil
}

func (rt *Runtime) CopyToRuntime(_ context.Context, runtimeID runtime.ID, src, dst string) error {
	dstPath, err := rt.runtimePath(runtimeID, dst)
	if err != nil {
		return err
	}

	if err = os.MkdirAll(filepath.Dir(dstPath), 0o755); err != nil {
		return fmt.Errorf("create dir for %s: %w", dstPath, err)
	}
	if err = copyFile(src, dstPath); err != nil {
		return fmt.Errorf("copy %s to runtime %s: %w", src, dst, err)
	}
	return nil
}

func (rt *Runtime) CopyFromRuntime(_ context.Context, runtimeID runtime.ID, src, dst string) error {
	srcPath, err := rt.runtimePath(runtimeID, src)
	if err != nil {
		return err
	}

	if err = os.MkdirAll(filepath.Dir(dst), 0o755); err != nil {
		return fmt.Errorf("create dir for %s: %w", dst, err)
	}
	if err = copyFile(srcPath, dst); err != nil {
		return fmt.Errorf("copy runtime %s to %s: %w", src, dst, err)
	}
	return nil
}

func (rt *Runtime) RunCommand(ctx context.Context, runtimeID runtime.ID, cmd []string, params runtime.RunParams) error {
	workDir, err := rt.getWorkDir(runtimeID)
	if err != nil {
		return err
	}
	if len(cmd) == 0 {
		return fmt.Errorf("empty command")
	}

	ctxExec := ctx
	var cancel context.CancelFunc
	if params.Limits.Time != 0 {
		ctxExec, cancel = context.WithTimeout(ctx, time.Duration(params.Limits.Time))
		defer cancel()
	}

	execCmd := exec.CommandContext(ctxExec, cmd[0], cmd[1:]...)
	execCmd.Dir = workDir
	execCmd.Stderr = params.Stderr

	if params.StdinFile != "" {
		stdinPath, err := rt.runtimePath(runtimeID, params.StdinFile)
		if err != nil {
			return err
		}
		stdin, err := os.OpenFile(stdinPath, os.O_RDONLY, 0)
		if err != nil {
			return fmt.Errorf("open runtime stdin %s: %w", params.StdinFile, err)
		}
		defer func() { _ = stdin.Close() }()
		execCmd.Stdin = stdin
	}

	if params.StdoutFile != "" {
		stdoutPath, err := rt.runtimePath(runtimeID, params.StdoutFile)
		if err != nil {
			return err
		}
		if err := os.MkdirAll(filepath.Dir(stdoutPath), 0o755); err != nil {
			return fmt.Errorf("create dir for %s: %w", stdoutPath, err)
		}
		stdout, err := os.OpenFile(stdoutPath, os.O_CREATE|os.O_TRUNC|os.O_WRONLY, 0o644)
		if err != nil {
			return fmt.Errorf("open runtime stdout %s: %w", params.StdoutFile, err)
		}
		defer func() { _ = stdout.Close() }()
		execCmd.Stdout = stdout
	}

	if err := execCmd.Run(); err != nil {
		if errorsIsTimeout(ctxExec) {
			return runtime.ErrTimeout
		}
		var exitError *exec.ExitError
		if errors.As(err, &exitError) {
			return nil
		}
		return err
	}

	return nil
}

func (rt *Runtime) Stop(_ context.Context, runtimeID runtime.ID) error {
	rt.mu.Lock()
	workDir, ok := rt.workDir[runtimeID]
	if ok {
		delete(rt.workDir, runtimeID)
	}
	rt.mu.Unlock()

	if !ok {
		return nil
	}

	if err := os.RemoveAll(workDir); err != nil {
		return fmt.Errorf("remove local runtime dir: %w", err)
	}
	return nil
}

func (rt *Runtime) getWorkDir(runtimeID runtime.ID) (string, error) {
	if runtimeID == "" {
		return "", fmt.Errorf("runtime id is required")
	}

	rt.mu.Lock()
	workDir, ok := rt.workDir[runtimeID]
	rt.mu.Unlock()

	if !ok {
		return "", fmt.Errorf("runtime is not initialized")
	}

	return workDir, nil
}

func (rt *Runtime) runtimePath(runtimeID runtime.ID, path string) (string, error) {
	workDir, err := rt.getWorkDir(runtimeID)
	if err != nil {
		return "", err
	}
	if filepath.IsAbs(path) {
		return "", fmt.Errorf("runtime path must be relative: %s", path)
	}
	cleanPath := filepath.Clean(path)
	if cleanPath == ".." || len(cleanPath) >= 3 && cleanPath[:3] == "../" {
		return "", fmt.Errorf("invalid runtime path: %s", path)
	}
	return filepath.Join(workDir, cleanPath), nil
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
