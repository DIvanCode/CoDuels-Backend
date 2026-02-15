package provider

import (
	"context"
	"errors"
	"exesh/internal/config"
	"exesh/internal/domain/execution/source"
	"exesh/internal/domain/execution/source/sources"
	"fmt"
	"github.com/DIvanCode/filestorage/pkg/bucket"
	errs "github.com/DIvanCode/filestorage/pkg/errors"
	"io"
	"sync"
)

type (
	SourceProvider struct {
		cfg         config.SourceProviderConfig
		filestorage filestorage

		mu   sync.Mutex
		srcs map[source.ID]savedSource
	}

	savedSource struct {
		BucketID bucket.ID
		File     string
	}
)

func NewSourceProvider(cfg config.SourceProviderConfig, filestorage filestorage) *SourceProvider {
	return &SourceProvider{
		cfg:         cfg,
		filestorage: filestorage,

		mu:   sync.Mutex{},
		srcs: make(map[source.ID]savedSource),
	}
}

func (p *SourceProvider) SaveSource(ctx context.Context, src sources.Source) error {
	switch src.GetType() {
	case source.Inline:
		typedSrc := src.AsInline()

		sourceID := src.GetID()
		var bucketID bucket.ID
		if err := bucketID.FromString(sourceID.String()); err != nil {
			return fmt.Errorf("failed to calculate bucket id for inline input: %w", err)
		}
		file := bucketID.String()
		bucketTTL := p.cfg.FilestorageBucketTTL

		w, commit, abort, err := p.filestorage.CreateFile(ctx, bucketID, file, bucketTTL)
		if err != nil && errors.Is(err, errs.ErrFileAlreadyExists) {
			return nil
		}
		if err != nil {
			return fmt.Errorf("failed to create file: %w", err)
		}

		if _, err := w.Write([]byte(typedSrc.Content)); err != nil {
			_ = abort()
			return fmt.Errorf("failed to write content: %w", err)
		}

		if err := commit(); err != nil {
			_ = abort()
			return fmt.Errorf("failed to commit file creation: %w", err)
		}

		p.saveSource(src.GetID(), bucketID, file)
	case source.FilestorageBucketFile:
		typedSrc := src.AsFilestorageBucketFile()

		bucketID := typedSrc.BucketID
		file := typedSrc.File
		ttl := p.cfg.FilestorageBucketTTL
		downloadEndpoint := typedSrc.DownloadEndpoint

		if err := p.filestorage.DownloadFile(ctx, bucketID, file, ttl, downloadEndpoint); err != nil {
			return fmt.Errorf("failed to download file %s: %w", bucketID, err)
		}

		p.saveSource(src.GetID(), bucketID, file)
	default:
		return fmt.Errorf("unknown source type '%s'", src.GetType())
	}

	return nil
}

func (p *SourceProvider) saveSource(sourceID source.ID, bucketID bucket.ID, file string) {
	p.mu.Lock()
	defer p.mu.Unlock()

	p.srcs[sourceID] = savedSource{BucketID: bucketID, File: file}
}

func (p *SourceProvider) RemoveSource(ctx context.Context, src sources.Source) {
	p.mu.Lock()
	defer p.mu.Unlock()

	delete(p.srcs, src.GetID())
}

func (p *SourceProvider) getSavedSource(sourceID source.ID) (savedSource, bool) {
	p.mu.Lock()
	defer p.mu.Unlock()

	src, ok := p.srcs[sourceID]
	return src, ok
}

func (p *SourceProvider) Locate(ctx context.Context, sourceID source.ID) (path string, unlock func(), err error) {
	src, ok := p.getSavedSource(sourceID)
	if !ok {
		err = fmt.Errorf("source %s not found", sourceID.String())
		return
	}

	return p.filestorage.LocateFile(ctx, src.BucketID, src.File, p.cfg.FilestorageBucketTTL)
}

func (p *SourceProvider) Read(ctx context.Context, sourceID source.ID) (r io.Reader, unlock func(), err error) {
	src, ok := p.getSavedSource(sourceID)
	if !ok {
		err = fmt.Errorf("source %s not found", sourceID.String())
		return
	}

	return p.filestorage.ReadFile(ctx, src.BucketID, src.File, p.cfg.FilestorageBucketTTL)
}
