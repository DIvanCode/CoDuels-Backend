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
	"sync/atomic"
	"time"
)

type Runtime struct {
	binPath    string
	boxIDStart int
	boxIDCount int

	boxID   int
	boxRoot string
	boxDir  string
}

var nextBox uint32

type FuncOpt func(r *Runtime) error

func New() *Runtime {
	return &Runtime{
		binPath:    "isolate",
		boxIDStart: 0,
		boxIDCount: 1000,
	}
}

func (rt *Runtime) InitRuntime() error {
	if rt.boxRoot != "" {
		return nil
	}

	boxID, boxRoot, err := rt.initBox(context.Background())
	if err != nil {
		return err
	}

	rt.boxID = boxID
	rt.boxRoot = boxRoot
	rt.boxDir = filepath.Join(boxRoot, "box")
	return nil
}

func (rt *Runtime) CopyToRuntime(src, dst string) error {
	if rt.boxDir == "" {
		return fmt.Errorf("runtime is not initialized")
	}
	return copyFile(src, filepath.Join(rt.boxDir, dst))
}

func (rt *Runtime) CopyFromRuntime(src, dst string) error {
	if rt.boxDir == "" {
		return fmt.Errorf("runtime is not initialized")
	}
	return copyFile(filepath.Join(rt.boxDir, src), dst)
}

func (rt *Runtime) RunCommand(ctx context.Context, cmd []string, params runtime.RunParams) error {
	if len(cmd) == 0 {
		return fmt.Errorf("empty command")
	}
	if rt.boxRoot == "" {
		return fmt.Errorf("runtime is not initialized")
	}

	stderrFile := ".stderr"
	metaFile := ".meta"

	runArgs := []string{"-b", strconv.Itoa(rt.boxID), "--run"}
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
		runArgs = append(runArgs, "--stdin="+params.StdinFile)
	}
	if params.StdoutFile != "" {
		runArgs = append(runArgs, "--stdout="+params.StdoutFile)
	}
	runArgs = append(runArgs, "--stderr="+stderrFile)
	runArgs = append(runArgs, "--meta="+metaFile)
	runArgs = append(runArgs, "--")
	runArgs = append(runArgs, cmd...)

	runCmd := exec.CommandContext(ctx, rt.binPath, runArgs...)
	runCmd.Dir = rt.boxRoot
	var runStderr bytes.Buffer
	runCmd.Stderr = &runStderr

	runErr := runCmd.Run()

	if err := rt.handleMeta(filepath.Join(rt.boxDir, metaFile)); err != nil {
		return err
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

	if params.Stderr != nil {
		if err := copyFileToWriter(filepath.Join(rt.boxDir, stderrFile), params.Stderr); err != nil {
			return fmt.Errorf("copy stderr: %w", err)
		}
	}

	return nil
}

func (rt *Runtime) StopRuntime() error {
	if rt.boxRoot == "" {
		return nil
	}

	rt.cleanupBox(context.Background(), rt.boxID)
	rt.boxID = 0
	rt.boxRoot = ""
	rt.boxDir = ""
	return nil
}

func (rt *Runtime) initBox(ctx context.Context) (int, string, error) {
	start := int(atomic.AddUint32(&nextBox, 1))
	for i := 0; i < rt.boxIDCount; i++ {
		var stdout bytes.Buffer
		var stderr bytes.Buffer

		id := rt.boxIDStart + (start+i)%rt.boxIDCount
		args := []string{"--init", "-b", strconv.Itoa(id)}
		cmd := exec.CommandContext(ctx, rt.binPath, args...)
		cmd.Stdout = &stdout
		cmd.Stderr = &stderr

		if err := cmd.Run(); err != nil {
			errText := strings.TrimSpace(stderr.String())
			if strings.Contains(errText, "currently in use") {
				if ctx.Err() != nil {
					return 0, "", ctx.Err()
				}
				continue
			}
			return 0, "", fmt.Errorf("isolate init box %d: %w: %s", id, err, errText)
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

func (rt *Runtime) handleMeta(metaPath string) error {
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
