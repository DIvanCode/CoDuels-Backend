package docker

import (
	"archive/tar"
	"bytes"
	"context"
	"errors"
	"exesh/internal/runtime"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"strings"
	"time"

	"github.com/docker/docker/api/types/container"
	"github.com/docker/docker/api/types/network"
	"github.com/docker/docker/client"
	"github.com/docker/docker/pkg/stdcopy"
	v1 "github.com/opencontainers/image-spec/specs-go/v1"
)

type Runtime struct {
	client     *client.Client
	baseImage  string
	basePolicy policy

	stageDir        string
	lastContainerID string
}

type FuncOpt func(r *Runtime) error

func WithRestrictivePolicy() FuncOpt {
	return func(r *Runtime) error {
		r.basePolicy = restrictivePolicy
		return nil
	}
}

func WithDefaultClient() FuncOpt {
	return func(r *Runtime) error {
		c, err := client.NewClientWithOpts()
		if err != nil {
			return fmt.Errorf("docker client create: %w", err)
		}
		r.client = c
		return nil
	}
}

func WithBaseImage(baseImage string) FuncOpt {
	return func(r *Runtime) error {
		r.baseImage = baseImage
		return nil
	}
}

func New(opts ...FuncOpt) (*Runtime, error) {
	r := &Runtime{}
	for _, opt := range opts {
		err := opt(r)
		if err != nil {
			return nil, err
		}
	}
	return r, nil
}

func (r *Runtime) Init(_ context.Context) error {
	if r.stageDir != "" {
		return nil
	}
	stageDir, err := os.MkdirTemp("/tmp", "exesh-docker-*")
	if err != nil {
		return fmt.Errorf("create docker runtime stage dir: %w", err)
	}
	r.stageDir = stageDir
	return nil
}

func (r *Runtime) CopyToRuntime(_ context.Context, src, dst string) error {
	stagePath, err := r.stagePath(dst)
	if err != nil {
		return err
	}

	if err := os.MkdirAll(filepath.Dir(stagePath), 0o755); err != nil {
		return fmt.Errorf("create dir for %s: %w", stagePath, err)
	}
	if err := copyFile(src, stagePath); err != nil {
		return fmt.Errorf("copy %s to runtime stage %s: %w", src, dst, err)
	}
	return nil
}

func (r *Runtime) CopyFromRuntime(ctx context.Context, src, dst string) error {
	if r.lastContainerID == "" {
		return fmt.Errorf("runtime has no container with command results")
	}

	if err := os.MkdirAll(filepath.Dir(dst), 0o755); err != nil {
		return fmt.Errorf("create dir for %s: %w", dst, err)
	}

	srcPath, err := r.runtimePath(src)
	if err != nil {
		return err
	}
	if err := copyFromContainer(ctx, r.client, r.lastContainerID, srcPath, dst); err != nil {
		return fmt.Errorf("copy runtime %s to %s: %w", src, dst, err)
	}
	return nil
}

func (r *Runtime) RunCommand(ctx context.Context, cmd []string, params runtime.RunParams) error {
	if r.stageDir == "" {
		return fmt.Errorf("runtime is not initialized")
	}
	if len(cmd) == 0 {
		return fmt.Errorf("empty command")
	}

	if err := r.cleanupContainer(ctx); err != nil {
		return err
	}

	hostConfig := &container.HostConfig{}
	if r.basePolicy != nil {
		r.basePolicy(hostConfig)
	}
	cpuPolicy(int64(params.Limits.Time) / int64(time.Second))(hostConfig)
	memoryPolicy(int64(params.Limits.Memory))(hostConfig)

	cr, err := r.client.ContainerCreate(ctx,
		&container.Config{
			Image:      r.baseImage,
			Cmd:        cmd,
			WorkingDir: "/tmp",
			OpenStdin:  true,
			StdinOnce:  true,
		},
		hostConfig,
		&network.NetworkingConfig{},
		&v1.Platform{OS: "linux", Architecture: "amd64"},
		"")
	if err != nil {
		return fmt.Errorf("create docker container: %w", err)
	}
	r.lastContainerID = cr.ID

	if err := filepath.Walk(r.stageDir, func(path string, info os.FileInfo, walkErr error) error {
		if walkErr != nil {
			return walkErr
		}
		if info.IsDir() {
			return nil
		}
		rel, err := filepath.Rel(r.stageDir, path)
		if err != nil {
			return err
		}
		runtimePath, err := r.runtimePath(rel)
		if err != nil {
			return err
		}
		if err := copyToContainer(ctx, r.client, cr.ID, path, runtimePath); err != nil {
			return err
		}
		return nil
	}); err != nil {
		return fmt.Errorf("copy staged files to container: %w", err)
	}

	hjr, err := r.client.ContainerAttach(ctx, cr.ID, container.AttachOptions{
		Stream: true,
		Stdin:  true,
		Stdout: true,
		Stderr: true,
	})
	if err != nil {
		return fmt.Errorf("attach to container io streams: %w", err)
	}
	defer hjr.Close()

	if params.StdinFile != "" {
		stdinPath, err := r.stagePath(params.StdinFile)
		if err != nil {
			return err
		}
		fr, err := os.OpenFile(stdinPath, os.O_RDONLY, 0)
		if err != nil {
			return fmt.Errorf("open runtime stdin %s: %w", params.StdinFile, err)
		}
		defer func() { _ = fr.Close() }()

		go func(rd io.Reader) {
			_, _ = io.Copy(hjr.Conn, rd)
			defer func() { _ = hjr.CloseWrite() }()
		}(fr)
	}

	if err = r.client.ContainerStart(ctx, cr.ID, container.StartOptions{}); err != nil {
		return fmt.Errorf("start container: %w", err)
	}

	timeout := 10 * time.Second
	if params.Limits.Time != 0 {
		timeout = 5 * time.Duration(params.Limits.Time)
	}
	ctxTimeout, cancel := context.WithTimeout(ctx, timeout)
	defer cancel()

	var insp container.InspectResponse
	for {
		insp, err = r.client.ContainerInspect(ctxTimeout, cr.ID)
		if err != nil {
			return fmt.Errorf("inspect container: %w", err)
		}

		if !insp.State.Running {
			break
		}

		select {
		case <-ctxTimeout.Done():
			if errors.Is(ctxTimeout.Err(), context.DeadlineExceeded) {
				return runtime.ErrTimeout
			}
			return ctx.Err()
		case <-time.After(100 * time.Millisecond):
		}
	}

	if insp.State.ExitCode == 137 {
		if insp.State.OOMKilled {
			return runtime.ErrOutOfMemory
		}
		return runtime.ErrTimeout
	}

	stdout := bytes.NewBuffer(nil)
	if _, err = stdcopy.StdCopy(stdout, params.Stderr, hjr.Conn); err != nil {
		return fmt.Errorf("copy std streams from container: %w", err)
	}
	if params.StdoutFile != "" {
		stdoutPath, err := r.stagePath(params.StdoutFile)
		if err != nil {
			return err
		}
		if err := os.MkdirAll(filepath.Dir(stdoutPath), 0o755); err != nil {
			return fmt.Errorf("create dir for %s: %w", stdoutPath, err)
		}

		w, err := os.OpenFile(stdoutPath, os.O_WRONLY|os.O_CREATE|os.O_TRUNC, 0o644)
		if err != nil {
			return fmt.Errorf("open stdout file %s: %w", params.StdoutFile, err)
		}
		defer func() { _ = w.Close() }()

		if _, err := io.Copy(w, stdout); err != nil {
			return fmt.Errorf("copy stdout to stdout file: %w", err)
		}
	}

	if insp.State.ExitCode != 0 {
		stateErr := strings.TrimSpace(insp.State.Error)
		if stateErr != "" {
			return fmt.Errorf("container exited with code %d: %s", insp.State.ExitCode, stateErr)
		}
		return fmt.Errorf("container exited with code %d", insp.State.ExitCode)
	}

	return nil
}

func (r *Runtime) Stop(ctx context.Context) error {
	stopErr := r.cleanupContainer(ctx)

	if r.stageDir != "" {
		if err := os.RemoveAll(r.stageDir); err != nil {
			stopErr = errors.Join(stopErr, fmt.Errorf("remove docker runtime stage dir: %w", err))
		}
		r.stageDir = ""
	}
	return stopErr
}

func (r *Runtime) cleanupContainer(ctx context.Context) error {
	if r.lastContainerID == "" {
		return nil
	}
	err := r.client.ContainerRemove(ctx, r.lastContainerID, container.RemoveOptions{
		Force:         true,
		RemoveVolumes: true,
	})
	r.lastContainerID = ""
	if err != nil {
		return fmt.Errorf("remove container: %w", err)
	}
	return nil
}

func (r *Runtime) stagePath(path string) (string, error) {
	if r.stageDir == "" {
		return "", fmt.Errorf("runtime is not initialized")
	}
	if filepath.IsAbs(path) {
		return "", fmt.Errorf("runtime path must be relative: %s", path)
	}
	cleanPath := filepath.Clean(path)
	if cleanPath == ".." || strings.HasPrefix(cleanPath, "../") {
		return "", fmt.Errorf("invalid runtime path: %s", path)
	}
	return filepath.Join(r.stageDir, cleanPath), nil
}

func (r *Runtime) runtimePath(path string) (string, error) {
	if filepath.IsAbs(path) {
		return "", fmt.Errorf("runtime path must be relative: %s", path)
	}
	cleanPath := filepath.Clean(path)
	if cleanPath == ".." || strings.HasPrefix(cleanPath, "../") {
		return "", fmt.Errorf("invalid runtime path: %s", path)
	}
	return filepath.Join("/tmp", cleanPath), nil
}

func copyToContainer(ctx context.Context, dockerClient *client.Client, containerID, src, dst string) error {
	fr, err := os.OpenFile(src, os.O_RDONLY, 0)
	if err != nil {
		return fmt.Errorf("open file %s: %w", src, err)
	}
	defer func() { _ = fr.Close() }()

	sz, err := fr.Seek(0, io.SeekEnd)
	if err != nil {
		return fmt.Errorf("seek: %w", err)
	}

	if _, err = fr.Seek(0, io.SeekStart); err != nil {
		return fmt.Errorf("seek: %w", err)
	}

	buf := bytes.NewBuffer(nil)
	tw := tar.NewWriter(buf)
	defer func() { _ = tw.Close() }()

	if err = tw.WriteHeader(&tar.Header{
		Name:    filepath.Base(dst),
		Size:    sz,
		Mode:    0o755,
		ModTime: time.Now(),
		Format:  tar.FormatGNU,
	}); err != nil {
		return fmt.Errorf("write tar header: %w", err)
	}

	if _, err = io.Copy(tw, fr); err != nil {
		return fmt.Errorf("write file to tar: %w", err)
	}

	if err = dockerClient.CopyToContainer(ctx, containerID, filepath.Dir(dst), buf, container.CopyToContainerOptions{}); err != nil {
		return fmt.Errorf("copy file %s to container: %w", dst, err)
	}

	return nil
}

func copyFromContainer(ctx context.Context, dockerClient *client.Client, containerID, src, dst string) error {
	w, err := os.OpenFile(dst, os.O_WRONLY|os.O_CREATE|os.O_TRUNC, 0o644)
	if err != nil {
		return fmt.Errorf("open file %s: %w", dst, err)
	}
	defer func() { _ = w.Close() }()

	rc, _, err := dockerClient.CopyFromContainer(ctx, containerID, src)
	if err != nil {
		return fmt.Errorf("copy file %s from container: %w", src, err)
	}
	defer func() { _ = rc.Close() }()

	tr := tar.NewReader(rc)
	hdr, err := tr.Next()
	if err != nil {
		return fmt.Errorf("read tar header: %w", err)
	}

	if _, err = io.CopyN(w, tr, hdr.Size); err != nil {
		return fmt.Errorf("read file from tar: %w", err)
	}
	return nil
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
