package isolate

import (
	"bytes"
	"context"
	"errors"
	"exesh/internal/runtime"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"strings"
	"sync"
	"time"
)

type box struct {
	ID   int
	Root string
}

type Runtime struct {
	binPath string
	boxID   int
	onStop  func()

	mu  sync.Mutex
	box *box
}

type FuncOpt func(r *Runtime) error

func NewWithBoxID(boxID int) *Runtime {
	return &Runtime{
		binPath: "isolate",
		boxID:   boxID,
	}
}

func NewWithBoxIDAndOnStop(boxID int, onStop func()) *Runtime {
	rt := NewWithBoxID(boxID)
	rt.onStop = onStop
	return rt
}

func New() *Runtime {
	return NewWithBoxID(0)
}

func (rt *Runtime) Init(ctx context.Context) error {
	boxID, boxRoot, err := rt.initBox(ctx)
	if err != nil {
		return err
	}

	rt.mu.Lock()
	rt.box = &box{ID: boxID, Root: boxRoot}
	rt.mu.Unlock()

	return nil
}

func (rt *Runtime) CopyToRuntime(_ context.Context, src, dst string) error {
	b, err := rt.getBox()
	if err != nil {
		return err
	}

	dstPath, err := runtimePath(b.Root, dst)
	if err != nil {
		return err
	}

	if err = os.MkdirAll(filepath.Dir(dstPath), 0o755); err != nil {
		return fmt.Errorf("create dir for %s: %w", dst, err)
	}
	if err = copyFile(src, dstPath); err != nil {
		return fmt.Errorf("copy %s to runtime %s: %w", src, dst, err)
	}
	return nil
}

func (rt *Runtime) CopyFromRuntime(_ context.Context, src, dst string) error {
	b, err := rt.getBox()
	if err != nil {
		return err
	}

	srcPath, err := runtimePath(b.Root, src)
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

func (rt *Runtime) RunCommand(ctx context.Context, cmd []string, params runtime.RunParams) error {
	b, err := rt.getBox()
	if err != nil {
		return err
	}
	if len(cmd) == 0 {
		return fmt.Errorf("empty command")
	}

	stderrFile := ".stderr"
	metaFile := ".meta"

	runArgs := []string{"-b", strconv.Itoa(b.ID), "--run"}
	if params.Limits.Time != 0 {
		secs := time.Duration(params.Limits.Time).Seconds()
		runArgs = append(runArgs, "--time="+formatSeconds(secs))
		runArgs = append(runArgs, "--wall-time="+formatSeconds(5*secs))
	}
	if params.Limits.Memory != 0 {
		memKB := (int64(params.Limits.Memory) + 1023) / 1024
		runArgs = append(runArgs, "--mem="+strconv.FormatInt(memKB, 10))
	}
	if params.StdinFile != "" {
		runArgs = append(runArgs, "--stdin="+filepath.Clean(params.StdinFile))
	}
	if params.StdoutFile != "" {
		runArgs = append(runArgs, "--stdout="+filepath.Clean(params.StdoutFile))
	}
	runArgs = append(runArgs, "--stderr="+stderrFile)
	runArgs = append(runArgs, "--meta="+metaFile)
	runArgs = append(runArgs, "--")
	runArgs = append(runArgs, cmd...)

	runCmd := exec.CommandContext(ctx, rt.binPath, runArgs...)
	runCmd.Dir = b.Root
	var runStderr bytes.Buffer
	runCmd.Stderr = &runStderr

	runErr := runCmd.Run()

	if err = handleMeta(filepath.Join(b.Root, "box", metaFile)); err != nil {
		return err
	}

	if params.Stderr != nil {
		if err = copyFileToWriter(filepath.Join(b.Root, "box", stderrFile), params.Stderr); err != nil {
			return fmt.Errorf("copy stderr: %w", err)
		}
	}

	if runErr != nil {
		if errors.Is(ctx.Err(), context.DeadlineExceeded) {
			return runtime.ErrTimeout
		}
		if ctx.Err() != nil {
			return ctx.Err()
		}
		return fmt.Errorf("isolate run: %w: %s", runErr, strings.TrimSpace(runStderr.String()))
	}

	return nil
}

func (rt *Runtime) Stop(ctx context.Context) error {
	rt.mu.Lock()
	b := rt.box
	onStop := rt.onStop
	rt.onStop = nil
	rt.box = nil
	rt.mu.Unlock()

	if b == nil {
		if onStop != nil {
			onStop()
		}
		return nil
	}

	rt.cleanupBox(ctx, b.ID)
	if onStop != nil {
		onStop()
	}
	return nil
}

func (rt *Runtime) getBox() (box, error) {
	rt.mu.Lock()
	b := rt.box
	rt.mu.Unlock()

	if b == nil {
		return box{}, fmt.Errorf("runtime is not initialized")
	}

	return *b, nil
}

func runtimePath(boxRoot string, path string) (string, error) {
	if boxRoot == "" {
		return "", fmt.Errorf("runtime is not initialized")
	}
	if filepath.IsAbs(path) {
		return "", fmt.Errorf("runtime path must be relative: %s", path)
	}
	cleanPath := filepath.Clean(path)
	if cleanPath == ".." || strings.HasPrefix(cleanPath, "../") {
		return "", fmt.Errorf("invalid runtime path: %s", path)
	}
	return filepath.Join(boxRoot, "box", cleanPath), nil
}

func (rt *Runtime) initBox(ctx context.Context) (int, string, error) {
	var stdout bytes.Buffer
	var stderr bytes.Buffer

	args := []string{"--init", "-b", strconv.Itoa(rt.boxID)}
	cmd := exec.CommandContext(ctx, rt.binPath, args...)
	cmd.Stdout = &stdout
	cmd.Stderr = &stderr

	if err := cmd.Run(); err != nil {
		return 0, "", fmt.Errorf("isolate init box %d: %w: %s", rt.boxID, err, strings.TrimSpace(stderr.String()))
	}

	boxRoot := strings.TrimSpace(stdout.String())
	if boxRoot == "" {
		return 0, "", fmt.Errorf("empty isolate box root for box %d", rt.boxID)
	}
	return rt.boxID, boxRoot, nil
}

func (rt *Runtime) cleanupBox(ctx context.Context, id int) {
	args := []string{"--cleanup", "-b", strconv.Itoa(id)}
	cmd := exec.CommandContext(ctx, rt.binPath, args...)
	_ = cmd.Run()
}

func handleMeta(metaPath string) error {
	b, err := os.ReadFile(metaPath)
	if err != nil {
		return nil
	}
	status := ""
	for _, line := range strings.Split(string(b), "\n") {
		if strings.HasPrefix(line, "status:") {
			status = strings.TrimSpace(strings.TrimPrefix(line, "status:"))
			break
		}
	}
	switch status {
	case "TO":
		return runtime.ErrTimeout
	case "ML":
		return runtime.ErrOutOfMemory
	default:
		return nil
	}
}

func formatSeconds(sec float64) string {
	if sec < 0 {
		sec = 0
	}
	return fmt.Sprintf("%.3f", sec)
}

func copyFileToWriter(src string, w io.Writer) error {
	in, err := os.Open(src)
	if err != nil {
		if os.IsNotExist(err) {
			return nil
		}
		return err
	}
	defer func() { _ = in.Close() }()
	_, err = io.Copy(w, in)
	return err
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
