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
	"slices"
	"strconv"
	"strings"
	"sync/atomic"
	"time"
)

type Runtime struct {
	binPath    string
	boxIDStart int
	boxIDCount int
	nextBox    uint32
}

type FuncOpt func(r *Runtime) error

func New() *Runtime {
	return &Runtime{
		binPath:    "isolate",
		boxIDStart: 0,
		boxIDCount: 1000,
	}
}

func (rt *Runtime) Execute(ctx context.Context, cmd []string, params runtime.ExecuteParams) error {
	if len(cmd) == 0 {
		return fmt.Errorf("empty command")
	}

	boxID, boxRoot, err := rt.initBox(ctx)
	if err != nil {
		return err
	}
	defer rt.cleanupBox(context.Background(), boxID)
	boxDir := filepath.Join(boxRoot, "box")

	for _, outsideLocation := range params.InFiles {
		i := slices.Index(cmd, outsideLocation)
		if i != -1 {
			cmd[i] = filepath.Base(outsideLocation)
		}

		location := filepath.Join(boxDir, filepath.Base(outsideLocation))
		if err := os.MkdirAll(filepath.Dir(location), 0o755); err != nil {
			return fmt.Errorf("create dir for %s: %w", location, err)
		}
		if err := copyFile(outsideLocation, location); err != nil {
			return fmt.Errorf("copy in file %s: %w", outsideLocation, err)
		}
	}
	for _, outsideLocation := range params.OutFiles {
		i := slices.Index(cmd, outsideLocation)
		if i != -1 {
			cmd[i] = filepath.Base(outsideLocation)
		}
	}

	var stdinFile string
	if params.StdinFile != "" {
		stdinFile = filepath.Base(params.StdinFile)
	}
	var stdoutFile string
	if params.StdoutFile != "" {
		stdoutFile = filepath.Base(params.StdoutFile)
	}
	stderrFile := ".stderr"
	metaFile := ".meta"

	runArgs := []string{"-b", strconv.Itoa(boxID), "--run"}
	if params.Limits.Time != 0 {
		secs := time.Duration(params.Limits.Time).Seconds()
		runArgs = append(runArgs, "--time="+formatSeconds(secs))
		runArgs = append(runArgs, "--wall-time="+formatSeconds(5*secs))
	}
	if params.Limits.Memory != 0 {
		memKB := (int64(params.Limits.Memory) + 1023) / 1024
		runArgs = append(runArgs, "--mem="+strconv.FormatInt(memKB, 10))
	}
	if stdinFile != "" {
		runArgs = append(runArgs, "--stdin="+stdinFile)
	}
	if stdoutFile != "" {
		runArgs = append(runArgs, "--stdout="+stdoutFile)
	}
	runArgs = append(runArgs, "--stderr="+stderrFile)
	runArgs = append(runArgs, "--meta="+metaFile)
	runArgs = append(runArgs, "--")
	runArgs = append(runArgs, cmd...)

	runCmd := exec.CommandContext(ctx, rt.binPath, runArgs...)
	runCmd.Dir = boxRoot
	var runStderr bytes.Buffer
	runCmd.Stderr = &runStderr

	fmt.Println(runCmd.String())
	runErr := runCmd.Run()

	if err := rt.handleMeta(filepath.Join(boxDir, metaFile)); err != nil {
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

	if stdoutFile != "" {
		if err := copyFile(filepath.Join(boxDir, stdoutFile), params.StdoutFile); err != nil {
			return fmt.Errorf("copy stdout: %w", err)
		}
	}
	if params.Stderr != nil {
		if err := copyFileToWriter(filepath.Join(boxDir, stderrFile), params.Stderr); err != nil {
			return fmt.Errorf("copy stderr: %w", err)
		}
	}

	for _, location := range params.OutFiles {
		if err := os.MkdirAll(filepath.Dir(location), 0o755); err != nil {
			return fmt.Errorf("create dir for %s: %w", location, err)
		}
		if err := copyFile(filepath.Join(boxDir, filepath.Base(location)), location); err != nil {
			return fmt.Errorf("copy out file %s: %w", location, err)
		}
	}

	return nil
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
	fmt.Printf("copy %s to %s\n", src, dst)

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
