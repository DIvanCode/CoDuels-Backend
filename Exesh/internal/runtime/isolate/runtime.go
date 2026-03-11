package isolate

import (
	"bytes"
	"context"
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

	insideLocation := func(outsideLocation string) string {
		return filepath.Join(boxDir, filepath.Base(outsideLocation))
	}

	for _, location := range params.InFiles {
		i := slices.Index(cmd, location)
		if i != -1 {
			cmd[i] = insideLocation(location)
		}
	}
	for _, location := range params.OutFiles {
		i := slices.Index(cmd, location)
		if i != -1 {
			cmd[i] = insideLocation(location)
		}
	}

	var stdoutPath string
	if params.Stdout != nil {
		stdoutPath = filepath.Join(boxDir, ".stdout")
	}
	stderrPath := filepath.Join(boxDir, ".stderr")
	metaPath := filepath.Join(boxDir, ".meta")

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
	if stdoutPath != "" {
		runArgs = append(runArgs, "--stdout="+stdoutPath)
	}
	runArgs = append(runArgs, "--stderr="+stderrPath)
	for _, d := range rt.dirs {
		if d != "" {
			runArgs = append(runArgs, "--dir="+d)
		}
	}
	runArgs = append(runArgs, "--meta="+metaPath)
	runArgs = append(runArgs, "--")

	mappedCmd := make([]string, len(cmd))
	for i, arg := range cmd {
		if mapped, ok := sandboxMap[arg]; ok {
			mappedCmd[i] = mapped
			continue
		}
		mappedCmd[i] = arg
	}

	if len(cmd) > 0 {
		if _, ok := sandboxMap[cmd[0]]; ok {
			cmdHostPath := hostPath(cmd[0])
			if st, err := os.Stat(cmdHostPath); err == nil && !st.IsDir() {
				_ = os.Chmod(cmdHostPath, st.Mode().Perm()|0o111)
			}
		}
	}
	runArgs = append(runArgs, mappedCmd...)

	runCmd := exec.CommandContext(ctx, rt.binPath, runArgs...)
	runCmd.Dir = boxRoot
	var runStdout bytes.Buffer
	var runStderr bytes.Buffer
	runCmd.Stdout = &runStdout
	runCmd.Stderr = &runStderr

	runErr := runCmd.Run()

	if err := rt.handleMeta(filepath.Join(boxRoot, metaPath)); err != nil {
		return err
	}

	if runErr != nil {
		if ctx.Err() == context.DeadlineExceeded {
			return runtime.ErrTimeout
		}
		if ctx.Err() != nil {
			return ctx.Err()
		}
		return fmt.Errorf("isolate run: %w: %s", runErr, strings.TrimSpace(runStderr.String()))
	}

	if params.Stdout != nil {
		if err := copyFileToWriter(filepath.Join(boxDir, ".stdout"), params.Stdout); err != nil {
			return fmt.Errorf("read stdout: %w", err)
		}
	}
	if params.Stderr != nil {
		if err := copyFileToWriter(filepath.Join(boxDir, stderrPath), params.Stderr); err != nil {
			return fmt.Errorf("read stderr: %w", err)
		}
	}

	for _, f := range params.OutFiles {
		src := hostPath(f.InsideLocation)
		if err := os.MkdirAll(filepath.Dir(f.OutsideLocation), 0o755); err != nil {
			return fmt.Errorf("create dir for %s: %w", f.OutsideLocation, err)
		}
		if err := copyFile(src, f.OutsideLocation); err != nil {
			return fmt.Errorf("copy out file %s: %w", f.OutsideLocation, err)
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
	defer in.Close()
	_, err = io.Copy(w, in)
	return err
}

func copyFile(src, dst string) error {
	in, err := os.Open(src)
	if err != nil {
		return err
	}
	defer in.Close()

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
	defer out.Close()

	if _, err := io.Copy(out, in); err != nil {
		return err
	}
	return out.Close()
}
