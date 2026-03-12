package local

import (
	"context"
	"errors"
	"exesh/internal/runtime"
	"fmt"
	"os/exec"
	"time"
)

type Runtime struct{}

type FuncOpt func(r *Runtime) error

func New() *Runtime {
	return &Runtime{}
}

func (rt *Runtime) Execute(ctx context.Context, cmd []string, params runtime.ExecuteParams) error {
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
	execCmd.Stderr = params.Stderr

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

func errorsIsTimeout(ctx context.Context) bool {
	if ctx == nil {
		return false
	}
	return errors.Is(ctx.Err(), context.DeadlineExceeded)
}
