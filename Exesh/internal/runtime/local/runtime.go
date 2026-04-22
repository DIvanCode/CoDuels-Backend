package local

import (
	"context"
	"errors"
	"exesh/internal/runtime"
	"fmt"
	"io"
	"math"
	"os"
	"os/exec"
	"path/filepath"
	stdruntime "runtime"
	"sync"
	"syscall"
	"time"
)

type Runtime struct {
	mu      sync.Mutex
	workDir string
}

type FuncOpt func(r *Runtime) error

func New() *Runtime {
	return &Runtime{}
}

func (rt *Runtime) Init(_ context.Context) error {
	workDir, err := os.MkdirTemp("/tmp", "*")
	if err != nil {
		return fmt.Errorf("create local runtime dir: %w", err)
	}

	rt.mu.Lock()
	rt.workDir = workDir
	rt.mu.Unlock()

	return nil
}

func (rt *Runtime) CopyToRuntime(_ context.Context, src, dst string) error {
	dstPath, err := rt.runtimePath(dst)
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

func (rt *Runtime) CopyFromRuntime(_ context.Context, src, dst string) error {
	srcPath, err := rt.runtimePath(src)
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

func (rt *Runtime) RunCommand(ctx context.Context, cmd []string, params runtime.RunParams) (*runtime.Usage, error) {
	workDir, err := rt.getWorkDir()
	if err != nil {
		return nil, err
	}
	if len(cmd) == 0 {
		return nil, fmt.Errorf("empty command")
	}
	startedAt := time.Now()

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
		stdinPath, err := rt.runtimePath(params.StdinFile)
		if err != nil {
			return nil, err
		}
		stdin, err := os.OpenFile(stdinPath, os.O_RDONLY, 0)
		if err != nil {
			return nil, fmt.Errorf("open runtime stdin %s: %w", params.StdinFile, err)
		}
		defer func() { _ = stdin.Close() }()
		execCmd.Stdin = stdin
	}

	if params.StdoutFile != "" {
		stdoutPath, err := rt.runtimePath(params.StdoutFile)
		if err != nil {
			return nil, err
		}
		if err := os.MkdirAll(filepath.Dir(stdoutPath), 0o755); err != nil {
			return nil, fmt.Errorf("create dir for %s: %w", stdoutPath, err)
		}
		stdout, err := os.OpenFile(stdoutPath, os.O_CREATE|os.O_TRUNC|os.O_WRONLY, 0o644)
		if err != nil {
			return nil, fmt.Errorf("open runtime stdout %s: %w", params.StdoutFile, err)
		}
		defer func() { _ = stdout.Close() }()
		execCmd.Stdout = stdout
	}

	if err := execCmd.Run(); err != nil {
		usage := runtime.Usage{
			ElapsedTime: int(time.Since(startedAt).Milliseconds()),
			UsedMemory:  processUsedMemory(execCmd.ProcessState),
		}
		if errorsIsTimeout(ctxExec) {
			return &usage, runtime.ErrTimeout
		}
		return &usage, err
	}

	return &runtime.Usage{
		ElapsedTime: int(time.Since(startedAt).Milliseconds()),
		UsedMemory:  processUsedMemory(execCmd.ProcessState),
	}, nil
}

func (rt *Runtime) Stop(_ context.Context) error {
	rt.mu.Lock()
	workDir := rt.workDir
	rt.workDir = ""
	rt.mu.Unlock()

	if workDir == "" {
		return nil
	}

	if err := os.RemoveAll(workDir); err != nil {
		return fmt.Errorf("remove local runtime dir: %w", err)
	}
	return nil
}

func (rt *Runtime) getWorkDir() (string, error) {
	rt.mu.Lock()
	workDir := rt.workDir
	rt.mu.Unlock()

	if workDir == "" {
		return "", fmt.Errorf("runtime is not initialized")
	}

	return workDir, nil
}

func processUsedMemory(state *os.ProcessState) int {
	if state == nil {
		return 0
	}

	sysUsage := state.SysUsage()
	rusage, ok := sysUsage.(*syscall.Rusage)
	if !ok || rusage == nil {
		return 0
	}

	maxRSS := float64(rusage.Maxrss)
	if maxRSS <= 0 {
		return 0
	}

	switch stdruntime.GOOS {
	case "darwin":
		// macOS reports ru_maxrss in bytes.
		return int(math.Ceil(maxRSS / (1024.0 * 1024.0)))
	default:
		// Linux reports ru_maxrss in KiB.
		return int(math.Ceil(maxRSS / 1024.0))
	}
}

func (rt *Runtime) runtimePath(path string) (string, error) {
	workDir, err := rt.getWorkDir()
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
