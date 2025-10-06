package runtime

import (
	"archive/tar"
	"bytes"
	"context"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"time"

	"github.com/docker/docker/api/types/container"
	"github.com/docker/docker/api/types/network"
	"github.com/docker/docker/api/types/strslice"
	"github.com/docker/docker/client"
	"github.com/docker/docker/pkg/stdcopy"
	v1 "github.com/opencontainers/image-spec/specs-go/v1"
)

type DockerRuntime struct {
	client       *client.Client
	baseImage    string
	networkAllow bool
	nproc        int64
}

type DockerRuntimeOpt func(dr *DockerRuntime) error

func WithDockerRestrictivePolicy() DockerRuntimeOpt {
	return func(dr *DockerRuntime) error {
		dr.networkAllow = false
		dr.nproc = 1
		return nil
	}
}

func WithDockerClient(client *client.Client) DockerRuntimeOpt {
	return func(dr *DockerRuntime) error {
		dr.client = client
		return nil
	}
}

func WithDockerDefaultClient() DockerRuntimeOpt {
	return func(dr *DockerRuntime) error {
		c, err := client.NewClientWithOpts()
		if err != nil {
			return fmt.Errorf("docker client create: %w", err)
		}
		dr.client = c
		return nil
	}
}

func WithDockerBaseImage(baseImage string) DockerRuntimeOpt {
	return func(dr *DockerRuntime) error {
		dr.baseImage = baseImage
		return nil
	}
}

func NewDockerRuntime(opts ...DockerRuntimeOpt) (*DockerRuntime, error) {
	dr := &DockerRuntime{}
	for _, opt := range opts {
		err := opt(dr)
		if err != nil {
			return nil, err
		}
	}
	return dr, nil
}

func (dr *DockerRuntime) Execute(ctx context.Context, params ExecuteParams) error {
	var networkMode container.NetworkMode = network.NetworkNone
	if dr.networkAllow {
		networkMode = network.NetworkDefault
	}

	timeLimitSecs := int64(params.Limits.Time) / int64(time.Second)

	cr, err := dr.client.ContainerCreate(ctx,
		&container.Config{
			Image:     dr.baseImage,
			Cmd:       strslice.StrSlice(params.Command),
			OpenStdin: true,
		},
		&container.HostConfig{
			NetworkMode: networkMode,
			Resources: container.Resources{
				Memory: int64(params.Limits.Memory),
				Ulimits: []*container.Ulimit{
					{
						// TODO: think about sleep(inf) case, it will not be killed
						Name: "cpu",
						Soft: timeLimitSecs,
						Hard: timeLimitSecs,
					},
					{
						Name: "nproc",
						Soft: dr.nproc,
						Hard: dr.nproc,
					},
				},
			},
		},
		&network.NetworkingConfig{},
		&v1.Platform{OS: "linux", Architecture: "amd64"},
		"")
	if err != nil {
		return fmt.Errorf("create docker container: %w", err)
	}

	defer dr.client.ContainerRemove(ctx, cr.ID, container.RemoveOptions{
		Force:         true,
		RemoveVolumes: true,
		RemoveLinks:   true,
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

	// TODO: think whether it will ever freeeze
	if params.Stdin != nil {
		go func(r io.Reader) { io.Copy(hjr.Conn, r) }(params.Stdin)
	}

	err = dr.client.ContainerStart(ctx, cr.ID, container.StartOptions{})
	if err != nil {
		return fmt.Errorf("start container: %w", err)
	}

	var insp container.InspectResponse
	for {
		insp, err = dr.client.ContainerInspect(ctx, cr.ID)
		if err != nil {
			return fmt.Errorf("inspect container: %w", err)
		}

		if !insp.State.Running {
			break
		}
	}

	if insp.State.ExitCode == 137 {
		if insp.State.OOMKilled {
			return ErrOutOfMemory
		}
		return ErrTimeout
	}

	stdcopy.StdCopy(params.Stdout, params.Stderr, hjr.Conn)

	if insp.State.ExitCode != 0 {
		return fmt.Errorf("unknown error exit code: %d", insp.State.ExitCode)
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
