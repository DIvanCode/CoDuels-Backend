package provider

import (
	"context"
	"exesh/internal/config"
	"exesh/internal/domain/execution/job"
	"fmt"
	"github.com/DIvanCode/filestorage/pkg/bucket"
	"io"
)

type OutputProvider struct {
	cfg         config.OutputProviderConfig
	filestorage filestorage
}

func NewOutputProvider(cfg config.OutputProviderConfig, filestorage filestorage) *OutputProvider {
	return &OutputProvider{
		cfg:         cfg,
		filestorage: filestorage,
	}
}

func (p *OutputProvider) Reserve(ctx context.Context, jobID job.ID, file string) (path string, commit, abort func() error, err error) {
	var bucketID bucket.ID
	if err = bucketID.FromString(jobID.String()); err != nil {
		err = fmt.Errorf("failed to create bucket id: %w", err)
		return
	}

	ttl := p.cfg.ArtifactTTL
	return p.filestorage.ReserveFile(ctx, bucketID, file, ttl)
}

func (p *OutputProvider) Read(ctx context.Context, jobID job.ID, file string) (r io.Reader, unlock func(), err error) {
	var bucketID bucket.ID
	if err = bucketID.FromString(jobID.String()); err != nil {
		err = fmt.Errorf("failed to create bucket id: %w", err)
		return
	}

	return p.filestorage.ReadFile(ctx, bucketID, file)
}

func (p *OutputProvider) Create(
	ctx context.Context,
	jobID job.ID,
	file string,
) (w io.Writer, commit, abort func() error, err error) {
	var bucketID bucket.ID
	if err = bucketID.FromString(jobID.String()); err != nil {
		err = fmt.Errorf("failed to create bucket id: %w", err)
		return
	}

	ttl := p.cfg.ArtifactTTL
	return p.filestorage.CreateFile(ctx, bucketID, file, ttl)
}
