package docker

import (
	"archive/tar"
	"bytes"
	"context"
	"errors"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"time"

	"exesh/internal/runtime"

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

type FuncOpt func(dr *Runtime) error

func WithRestrictivePolicy() FuncOpt {
	return func(dr *Runtime) error {
		dr.basePolicy = restrictivePolicy
		return nil
	}
}

func WithClient(client *client.Client) FuncOpt {
	return func(dr *Runtime) error {
		dr.client = client
		return nil
	}
}

func WithDefaultClient() FuncOpt {
	return func(dr *Runtime) error {
		c, err := client.NewClientWithOpts()
		if err != nil {
			return fmt.Errorf("docker client create: %w", err)
		}
		dr.client = c
		return nil
	}
}

func WithBaseImage(baseImage string) FuncOpt {
	return func(dr *Runtime) error {
		dr.baseImage = baseImage
		return nil
	}
}

func New(opts ...FuncOpt) (*Runtime, error) {
	dr := &Runtime{}
	for _, opt := range opts {
		err := opt(dr)
		if err != nil {
			return nil, err
		}
	}
	return dr, nil
}

func (dr *Runtime) Execute(ctx context.Context, cmd []string, params runtime.ExecuteParams) error {
	hostConfig := &container.HostConfig{}
	dr.basePolicy(hostConfig)
	cpuPolicy(int64(params.Limits.Time) / int64(time.Second))(hostConfig)
	memoryPolicy(int64(params.Limits.Memory))(hostConfig)

	// we do not know why, but without StdinOnce, without CloseWrite stdin is not closed, and with it - stdout is empty
	cr, err := dr.client.ContainerCreate(ctx,
		&container.Config{Image: dr.baseImage, Cmd: cmd, OpenStdin: true, StdinOnce: true},
		hostConfig,
		&network.NetworkingConfig{},
		&v1.Platform{OS: "linux", Architecture: "amd64"},
		"")
	if err != nil {
		return fmt.Errorf("create docker container: %w", err)
	}

	defer dr.client.ContainerRemove(ctx, cr.ID, container.RemoveOptions{
		Force:         true,
		RemoveVolumes: true,
	})

	for _, f := range params.InFiles {
		r, err := os.OpenFile(f.OutsideLocation, os.O_RDONLY, 0)
		if err != nil {
			return fmt.Errorf("open file %s: %w", f.OutsideLocation, err)
		}

		sz, err := r.Seek(0, io.SeekEnd)
		if err != nil {
			return fmt.Errorf("seek: %w", err)
		}

		_, err = r.Seek(0, io.SeekStart)
		if err != nil {
			return fmt.Errorf("seek: %w", err)
		}

		buf := bytes.NewBuffer(nil)
		tw := tar.NewWriter(buf)

		defer tw.Close()

		err = tw.WriteHeader(&tar.Header{Name: filepath.Base(f.InsideLocation), Size: sz, Mode: 0o755, ModTime: time.Now(), Format: tar.FormatGNU})
		if err != nil {
			return fmt.Errorf("write tar header: %w", err)
		}

		_, err = io.Copy(tw, r)
		if err != nil {
			return fmt.Errorf("write file to tar: %w", err)
		}

		err = dr.client.CopyToContainer(ctx, cr.ID, filepath.Dir(f.InsideLocation), buf, container.CopyToContainerOptions{})
		if err != nil {
			return fmt.Errorf("copy file %s to container: %w", f.OutsideLocation, err)
		}
	}

	hjr, err := dr.client.ContainerAttach(ctx, cr.ID, container.AttachOptions{
		Stream: true,
		Stdin:  true,
		Stdout: true,
		Stderr: true,
	})
	if err != nil {
		return fmt.Errorf("attach to container io streams: %w", err)
	}

	defer hjr.Close()

	// it will nto freeze because both hjr.Conn and params.Stdin will be closed at some point
	if params.Stdin != nil {
		go func(r io.Reader) {
			_, _ = io.Copy(hjr.Conn, params.Stdin)
			hjr.CloseWrite()
		}(params.Stdin)
	}

	err = dr.client.ContainerStart(ctx, cr.ID, container.StartOptions{})
	if err != nil {
		return fmt.Errorf("start container: %w", err)
	}

	// force larger deadline because the submission may just hang waiting for input
	timeout := 30 * time.Second
	if params.Limits.Time != 0 {
		timeout = 10 * time.Duration(params.Limits.Time)
	}
	ctxTimeout, cancel := context.WithTimeout(ctx, timeout)
	defer cancel()

	var insp container.InspectResponse
	for {
		insp, err = dr.client.ContainerInspect(ctxTimeout, cr.ID)
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
		case <-time.After(1 * time.Second):
		}
	}

	if insp.State.ExitCode == 137 {
		if insp.State.OOMKilled {
			return runtime.ErrOutOfMemory
		}
		return runtime.ErrTimeout
	}

	_, err = stdcopy.StdCopy(params.Stdout, params.Stderr, hjr.Conn)
	if err != nil {
		return fmt.Errorf("copy std streams from container: %w", err)
	}

	for _, f := range params.OutFiles {
		w, err := os.OpenFile(f.OutsideLocation, os.O_WRONLY|os.O_CREATE, 0o755)
		if err != nil {
			return fmt.Errorf("open file %s: %w", f.OutsideLocation, err)
		}

		defer w.Close()

		rc, _, err := dr.client.CopyFromContainer(ctx, cr.ID, f.InsideLocation)
		if err != nil {
			return fmt.Errorf("copy file %s from container: %w", f.InsideLocation, err)
		}

		defer rc.Close()

		tr := tar.NewReader(rc)
		hdr, err := tr.Next()
		if err != nil {
			return fmt.Errorf("read tar header: %w", err)
		}

		_, err = io.CopyN(w, tr, hdr.Size)
		if err != nil {
			return fmt.Errorf("read file from tar: %w", err)
		}

		_, err = io.Copy(w, rc)
		if err != nil {
			return fmt.Errorf("write file: %w", err)
		}
	}

	return nil
}
