package provider

import (
	"context"
	"errors"
	"exesh/internal/config"
	"exesh/internal/domain/execution/job"
	"fmt"
	"github.com/DIvanCode/filestorage/pkg/bucket"
	errs "github.com/DIvanCode/filestorage/pkg/errors"
	"io"
	"time"
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

func (p *OutputProvider) Reserve(
	ctx context.Context,
	jobID job.ID,
	file string,
) (path string, commit, abort func() (*time.Time, error), err error) {
	var bucketID bucket.ID
	if err = bucketID.FromString(jobID.String()); err != nil {
		err = fmt.Errorf("failed to create bucket id: %w", err)
		return
	}

	var filestorageCommit, filestorageAbort func() error
	path, filestorageCommit, filestorageAbort, err = p.filestorage.ReserveFile(ctx, bucketID, file, p.cfg.ArtifactTTL)

	commit = func() (*time.Time, error) {
		if commitErr := filestorageCommit(); err != nil && !errors.Is(err, errs.ErrFileAlreadyExists) {
			return nil, commitErr
		}

		trashTime, getTrashTimeErr := p.filestorage.GetBucketTrashTime(ctx, bucketID)
		if getTrashTimeErr != nil {
			return nil, fmt.Errorf("failed to get bucket output trash time: %w", err)
		}

		return trashTime, nil
	}

	abort = func() (*time.Time, error) {
		if err != nil && errors.Is(err, errs.ErrFileAlreadyExists) {
			trashTime, getTrashTimeErr := p.filestorage.GetBucketTrashTime(ctx, bucketID)
			if getTrashTimeErr != nil {
				return nil, fmt.Errorf("failed to get bucket output trash time: %w", err)
			}

			return trashTime, nil
		}

		return nil, filestorageAbort()
	}

	return path, commit, abort, err
}

func (p *OutputProvider) Read(ctx context.Context, jobID job.ID, file string) (r io.Reader, unlock func(), err error) {
	var bucketID bucket.ID
	if err = bucketID.FromString(jobID.String()); err != nil {
		err = fmt.Errorf("failed to create bucket id: %w", err)
		return
	}

	return p.filestorage.ReadFile(ctx, bucketID, file, p.cfg.ArtifactTTL)
}
