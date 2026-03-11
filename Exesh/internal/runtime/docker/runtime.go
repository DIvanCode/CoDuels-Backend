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
	"slices"
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

func (r *Runtime) Execute(ctx context.Context, cmd []string, params runtime.ExecuteParams) error {
	hostConfig := &container.HostConfig{}
	if r.basePolicy != nil {
		r.basePolicy(hostConfig)
	}
	cpuPolicy(int64(params.Limits.Time) / int64(time.Second))(hostConfig)
	memoryPolicy(int64(params.Limits.Memory))(hostConfig)

	for _, location := range params.InFiles {
		i := slices.Index(cmd, location)
		if i != -1 {
			cmd[i] = r.insideLocation(location)
		}
	}
	for _, location := range params.OutFiles {
		i := slices.Index(cmd, location)
		if i != -1 {
			cmd[i] = r.insideLocation(location)
		}
	}

	cr, err := r.client.ContainerCreate(ctx,
		&container.Config{Image: r.baseImage, Cmd: cmd, OpenStdin: true, StdinOnce: true},
		hostConfig,
		&network.NetworkingConfig{},
		&v1.Platform{OS: "linux", Architecture: "amd64"},
		"")
	if err != nil {
		return fmt.Errorf("create docker container: %w", err)
	}
	defer func() {
		_ = r.client.ContainerRemove(ctx, cr.ID, container.RemoveOptions{
			Force:         true,
			RemoveVolumes: true,
		})
	}()

	for _, location := range params.InFiles {
		copyFunc := func(outsideLocation, insideLocation string) error {
			fr, err := os.OpenFile(outsideLocation, os.O_RDONLY, 0)
			if err != nil {
				return fmt.Errorf("open file %s: %w", outsideLocation, err)
			}

			sz, err := fr.Seek(0, io.SeekEnd)
			if err != nil {
				return fmt.Errorf("seek: %w", err)
			}

			_, err = fr.Seek(0, io.SeekStart)
			if err != nil {
				return fmt.Errorf("seek: %w", err)
			}

			buf := bytes.NewBuffer(nil)
			tw := tar.NewWriter(buf)
			defer func() { _ = tw.Close() }()

			err = tw.WriteHeader(&tar.Header{
				Name:    filepath.Base(insideLocation),
				Size:    sz,
				Mode:    0o755,
				ModTime: time.Now(),
				Format:  tar.FormatGNU,
			})
			if err != nil {
				return fmt.Errorf("write tar header: %w", err)
			}

			if _, err = io.Copy(tw, fr); err != nil {
				return fmt.Errorf("write file to tar: %w", err)
			}

			err = r.client.CopyToContainer(ctx, cr.ID, filepath.Dir(insideLocation), buf, container.CopyToContainerOptions{})
			if err != nil {
				return fmt.Errorf("copy file %s to container: %w", insideLocation, err)
			}

			return nil
		}

		if err := copyFunc(location, r.insideLocation(location)); err != nil {
			return err
		}
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
		fr, err := os.OpenFile(params.StdinFile, os.O_RDONLY, 0)
		if err != nil {
			return fmt.Errorf("open file %s: %w", params.StdinFile, err)
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

	// force larger deadline because the submission may just hang waiting for input
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
	_, err = stdcopy.StdCopy(stdout, params.Stderr, hjr.Conn)
	if err != nil {
		return fmt.Errorf("copy std streams from container: %w", err)
	}
	if params.StdoutFile != "" {
		w, err := os.OpenFile(params.StdoutFile, os.O_WRONLY|os.O_CREATE, 0o755)
		if err != nil {
			return fmt.Errorf("open stdout file %s: %w", params.StdoutFile, err)
		}
		defer func() { _ = w.Close() }()

		if _, err := io.Copy(w, stdout); err != nil {
			_ = w.Close()
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

	for _, location := range params.OutFiles {
		copyFunc := func(insideLocation, outsideLocation string) error {
			fmt.Printf("insideLocation=\"%s\", outsideLocation=\"%s\"\n", insideLocation, outsideLocation)
			w, err := os.OpenFile(outsideLocation, os.O_WRONLY|os.O_CREATE, 0o755)
			if err != nil {
				return fmt.Errorf("open file %s: %w", outsideLocation, err)
			}
			defer func() { _ = w.Close() }()

			rc, _, err := r.client.CopyFromContainer(ctx, cr.ID, insideLocation)
			if err != nil {
				return fmt.Errorf("copy file %s from container: %w", insideLocation, err)
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

		if location != params.StdoutFile {
			if err := copyFunc(r.insideLocation(location), location); err != nil {
				return err
			}
		}
	}

	return nil
}

func (_ *Runtime) insideLocation(outsideLocation string) string {
	return filepath.Join("/tmp", filepath.Base(outsideLocation))
}
