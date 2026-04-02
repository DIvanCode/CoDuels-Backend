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
	"sync/atomic"
	"time"
)

type box struct {
	ID   int
	Root string
}

type Runtime struct {
	binPath    string
	boxIDStart int
	boxIDCount int
	nextBox    uint32
	nextID     uint64

	mu    sync.Mutex
	boxes map[runtime.ID]box
}

type FuncOpt func(r *Runtime) error

func New() *Runtime {
	return &Runtime{
		binPath:    "isolate",
		boxIDStart: 0,
		boxIDCount: 1000,
		boxes:      make(map[runtime.ID]box),
	}
}

func (rt *Runtime) Init(ctx context.Context) (runtime.ID, error) {
	runtimeID := runtime.ID(strconv.FormatUint(atomic.AddUint64(&rt.nextID, 1), 10))

	boxID, boxRoot, err := rt.initBox(ctx)
	if err != nil {
		return "", err
	}

	rt.mu.Lock()
	rt.boxes[runtimeID] = box{ID: boxID, Root: boxRoot}
	rt.mu.Unlock()

	return runtimeID, nil
}

func (rt *Runtime) CopyToRuntime(_ context.Context, runtimeID runtime.ID, src, dst string) error {
	b, err := rt.getBox(runtimeID)
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

func (rt *Runtime) CopyFromRuntime(_ context.Context, runtimeID runtime.ID, src, dst string) error {
	b, err := rt.getBox(runtimeID)
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

func (rt *Runtime) RunCommand(ctx context.Context, runtimeID runtime.ID, cmd []string, params runtime.RunParams) error {
	b, err := rt.getBox(runtimeID)
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

func (rt *Runtime) Stop(ctx context.Context, runtimeID runtime.ID) error {
	rt.mu.Lock()
	b, exists := rt.boxes[runtimeID]
	if exists {
		delete(rt.boxes, runtimeID)
	}
	rt.mu.Unlock()

	if !exists {
		return nil
	}

	rt.cleanupBox(ctx, b.ID)
	return nil
}

func (rt *Runtime) getBox(runtimeID runtime.ID) (box, error) {
	if runtimeID == "" {
		return box{}, fmt.Errorf("runtime id is required")
	}

	rt.mu.Lock()
	b, exists := rt.boxes[runtimeID]
	rt.mu.Unlock()

	if !exists {
		return box{}, fmt.Errorf("runtime is not initialized")
	}

	return b, nil
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
	start := int(atomic.AddUint32(&rt.nextBox, 1))
	for i := 0; i < rt.boxIDCount; i++ {
		var stdout bytes.Buffer
		var stderr bytes.Buffer

		id := rt.boxIDStart + (start+i)%rt.boxIDCount
		args := []string{"--init", "-b", strconv.Itoa(id)}
		cmd := exec.CommandContext(ctx, rt.binPath, args...)
		cmd.Stdout = &stdout
		cmd.Stderr = &stderr

		if err := cmd.Run(); err != nil {
			return 0, "", fmt.Errorf("isolate init box %d: %w: %s", id, err, strings.TrimSpace(stderr.String()))
		}

		boxRoot := strings.TrimSpace(stdout.String())
		if boxRoot != "" {
			return id, boxRoot, nil
		}

		if ctx.Err() != nil {
			return 0, "", ctx.Err()
		}
	}
	return 0, "", fmt.Errorf("no available isolate boxes")
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
