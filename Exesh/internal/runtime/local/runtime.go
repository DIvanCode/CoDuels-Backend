package local

import (
	"context"
	"exesh/internal/runtime"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"time"
)

type Runtime struct {
	baseDir string
}

type FuncOpt func(r *Runtime) error

func WithBaseDir(dir string) FuncOpt {
	return func(r *Runtime) error {
		r.baseDir = dir
		return nil
	}
}

func New(opts ...FuncOpt) (*Runtime, error) {
	rt := &Runtime{}
	for _, opt := range opts {
		if err := opt(rt); err != nil {
			return nil, err
		}
	}
	return rt, nil
}

func (rt *Runtime) Execute(ctx context.Context, cmd []string, params runtime.ExecuteParams) error {
	if len(cmd) == 0 {
		return fmt.Errorf("empty command")
	}

	workDir, err := os.MkdirTemp(rt.baseDir, "exesh-")
	if err != nil {
		return fmt.Errorf("create work dir: %w", err)
	}
	defer os.RemoveAll(workDir)

	pathMap := make(map[string]string, len(params.InFiles)+len(params.OutFiles))
	mapPath := func(p string) string {
		if p == "" {
			return ""
		}
		if mapped, ok := pathMap[p]; ok {
			return mapped
		}
		clean := filepath.Clean(p)
		if filepath.IsAbs(clean) {
			clean = strings.TrimPrefix(clean, string(filepath.Separator))
		}
		mapped := filepath.Join(workDir, clean)
		pathMap[p] = mapped
		return mapped
	}

	for _, f := range params.InFiles {
		dst := mapPath(f.InsideLocation)
		if err := os.MkdirAll(filepath.Dir(dst), 0o755); err != nil {
			return fmt.Errorf("create dir for %s: %w", dst, err)
		}
		if err := copyFile(f.OutsideLocation, dst); err != nil {
			return fmt.Errorf("copy in file %s: %w", f.OutsideLocation, err)
		}
	}

	for _, f := range params.OutFiles {
		dst := mapPath(f.InsideLocation)
		if err := os.MkdirAll(filepath.Dir(dst), 0o755); err != nil {
			return fmt.Errorf("create dir for %s: %w", dst, err)
		}
	}

	mappedCmd := make([]string, len(cmd))
	for i, arg := range cmd {
		if mapped, ok := pathMap[arg]; ok {
			mappedCmd[i] = mapped
			continue
		}
		mappedCmd[i] = arg
	}

	ctxExec := ctx
	var cancel context.CancelFunc
	if params.Limits.Time != 0 {
		ctxExec, cancel = context.WithTimeout(ctx, time.Duration(params.Limits.Time))
		defer cancel()
	}

	execCmd := exec.CommandContext(ctxExec, mappedCmd[0], mappedCmd[1:]...)
	execCmd.Dir = workDir
	execCmd.Stdin = params.Stdin
	execCmd.Stdout = params.Stdout
	execCmd.Stderr = params.Stderr

	if err := execCmd.Run(); err != nil {
		if errorsIsTimeout(ctxExec) {
			return runtime.ErrTimeout
		}
		if _, ok := err.(*exec.ExitError); ok {
			return nil
		}
		return err
	}

	for _, f := range params.OutFiles {
		src := mapPath(f.InsideLocation)
		if err := os.MkdirAll(filepath.Dir(f.OutsideLocation), 0o755); err != nil {
			return fmt.Errorf("create dir for %s: %w", f.OutsideLocation, err)
		}
		if err := copyFile(src, f.OutsideLocation); err != nil {
			return fmt.Errorf("copy out file %s: %w", f.OutsideLocation, err)
		}
	}

	return nil
}

func errorsIsTimeout(ctx context.Context) bool {
	if ctx == nil {
		return false
	}
	return ctx.Err() == context.DeadlineExceeded
}

func copyFile(src, dst string) error {
	in, err := os.Open(src)
	if err != nil {
		return err
	}
	defer in.Close()

	out, err := os.OpenFile(dst, os.O_CREATE|os.O_TRUNC|os.O_WRONLY, 0o755)
	if err != nil {
		return err
	}
	defer out.Close()

	if _, err := io.Copy(out, in); err != nil {
		return err
	}
	return out.Close()
}
