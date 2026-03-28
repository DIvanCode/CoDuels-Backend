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

	containerID string
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

func (r *Runtime) InitRuntime() error {
	if r.containerID != "" {
		return nil
	}
	if r.client == nil {
		return fmt.Errorf("docker client is not configured")
	}

	hostConfig := &container.HostConfig{}
	if r.basePolicy != nil {
		r.basePolicy(hostConfig)
	}

	cr, err := r.client.ContainerCreate(context.Background(),
		&container.Config{
			Image: r.baseImage,
			Cmd:   []string{"sh", "-lc", "while true; do sleep 3600; done"},
			Tty:   false,
		},
		hostConfig,
		&network.NetworkingConfig{},
		&v1.Platform{OS: "linux", Architecture: "amd64"},
		"")
	if err != nil {
		return fmt.Errorf("create docker container: %w", err)
	}

	if err = r.client.ContainerStart(context.Background(), cr.ID, container.StartOptions{}); err != nil {
		_ = r.client.ContainerRemove(context.Background(), cr.ID, container.RemoveOptions{Force: true, RemoveVolumes: true})
		return fmt.Errorf("start docker container: %w", err)
	}

	r.containerID = cr.ID
	return nil
}

func (r *Runtime) CopyToRuntime(src, dst string) error {
	if r.containerID == "" {
		return fmt.Errorf("runtime is not initialized")
	}
	if err := r.execSimple(context.Background(), []string{"mkdir", "-p", filepath.Dir(r.insideLocation(dst))}); err != nil {
		return fmt.Errorf("create runtime dir for %s: %w", dst, err)
	}

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
	if err = tw.Close(); err != nil {
		return fmt.Errorf("close tar writer: %w", err)
	}

	if err = r.client.CopyToContainer(context.Background(), r.containerID, filepath.Dir(r.insideLocation(dst)), buf, container.CopyToContainerOptions{}); err != nil {
		return fmt.Errorf("copy file %s to container: %w", dst, err)
	}

	return nil
}

func (r *Runtime) CopyFromRuntime(src, dst string) error {
	if r.containerID == "" {
		return fmt.Errorf("runtime is not initialized")
	}

	if err := os.MkdirAll(filepath.Dir(dst), 0o755); err != nil {
		return fmt.Errorf("create dst dir: %w", err)
	}

	w, err := os.OpenFile(dst, os.O_WRONLY|os.O_CREATE|os.O_TRUNC, 0o755)
	if err != nil {
		return fmt.Errorf("open file %s: %w", dst, err)
	}
	defer func() { _ = w.Close() }()

	rc, _, err := r.client.CopyFromContainer(context.Background(), r.containerID, r.insideLocation(src))
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
	if _, err = io.Copy(w, rc); err != nil {
		return fmt.Errorf("write file: %w", err)
	}

	return nil
}

func (r *Runtime) RunCommand(ctx context.Context, cmd []string, params runtime.RunParams) error {
	if r.containerID == "" {
		return fmt.Errorf("runtime is not initialized")
	}
	if len(cmd) == 0 {
		return fmt.Errorf("empty command")
	}

	shellCmd := shellQuote(cmd[0])
	for _, arg := range cmd[1:] {
		shellCmd += " " + shellQuote(arg)
	}
	if params.StdinFile != "" {
		shellCmd += " < " + shellQuote(params.StdinFile)
	}
	if params.StdoutFile != "" {
		shellCmd += " > " + shellQuote(params.StdoutFile)
	}

	execConfig := &container.ExecOptions{
		Cmd:          []string{"sh", "-lc", shellCmd},
		AttachStdout: true,
		AttachStderr: true,
		WorkingDir:   "/tmp",
	}

	created, err := r.client.ContainerExecCreate(ctx, r.containerID, *execConfig)
	if err != nil {
		return fmt.Errorf("create container exec: %w", err)
	}

	attached, err := r.client.ContainerExecAttach(ctx, created.ID, container.ExecAttachOptions{})
	if err != nil {
		return fmt.Errorf("attach to exec: %w", err)
	}
	defer attached.Close()

	stdout := bytes.NewBuffer(nil)
	if _, err = stdcopy.StdCopy(stdout, params.Stderr, attached.Reader); err != nil {
		return fmt.Errorf("copy std streams from container: %w", err)
	}

	inspect, err := r.client.ContainerExecInspect(ctx, created.ID)
	if err != nil {
		return fmt.Errorf("inspect exec: %w", err)
	}
	if inspect.ExitCode == 137 {
		return runtime.ErrTimeout
	}
	if inspect.ExitCode != 0 {
		return fmt.Errorf("container exec exited with code %d", inspect.ExitCode)
	}

	return nil
}

func (r *Runtime) StopRuntime() error {
	if r.containerID == "" {
		return nil
	}
	err := r.client.ContainerRemove(context.Background(), r.containerID, container.RemoveOptions{
		Force:         true,
		RemoveVolumes: true,
	})
	r.containerID = ""
	return err
}

func (_ *Runtime) insideLocation(runtimePath string) string {
	return filepath.Join("/tmp", runtimePath)
}

func shellQuote(s string) string {
	return "'" + strings.ReplaceAll(s, "'", "'\"'\"'") + "'"
}

func (r *Runtime) execSimple(ctx context.Context, cmd []string) error {
	created, err := r.client.ContainerExecCreate(ctx, r.containerID, container.ExecOptions{
		Cmd:        cmd,
		WorkingDir: "/tmp",
	})
	if err != nil {
		return fmt.Errorf("create container exec: %w", err)
	}
	if err = r.client.ContainerExecStart(ctx, created.ID, container.ExecStartOptions{}); err != nil {
		return fmt.Errorf("start container exec: %w", err)
	}
	inspect, err := r.client.ContainerExecInspect(ctx, created.ID)
	if err != nil {
		return fmt.Errorf("inspect exec: %w", err)
	}
	if inspect.ExitCode != 0 {
		return fmt.Errorf("container exec exited with code %d", inspect.ExitCode)
	}
	return nil
}

func isContextTimeout(err error) bool {
	return errors.Is(err, context.DeadlineExceeded)
}

func trimStateError(err string) string {
	return strings.TrimSpace(err)
}
