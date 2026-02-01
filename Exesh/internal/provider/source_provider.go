package provider

import (
	"context"
	"exesh/internal/config"
	"exesh/internal/domain/execution/source"
	"exesh/internal/domain/execution/source/sources"
	"fmt"
	"github.com/DIvanCode/filestorage/pkg/bucket"
	"io"
	"sync"
)

type SourceProvider struct {
	cfg         config.SourceProviderConfig
	filestorage filestorage

	mu   sync.Mutex
	srcs map[source.ID]string
}

func NewSourceProvider(cfg config.SourceProviderConfig, filestorage filestorage) *SourceProvider {
	return &SourceProvider{
		cfg:         cfg,
		filestorage: filestorage,

		mu:   sync.Mutex{},
		srcs: make(map[source.ID]string),
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

		p.saveSource(src.GetID(), file)
	case source.FilestorageBucketFile:
		typedSrc := src.AsFilestorageBucketFile()

		bucketID := typedSrc.BucketID
		file := typedSrc.File
		ttl := p.cfg.FilestorageBucketTTL
		downloadEndpoint := typedSrc.DownloadEndpoint

		if err := p.filestorage.DownloadFile(ctx, bucketID, file, ttl, downloadEndpoint); err != nil {
			return fmt.Errorf("failed to download file %s: %w", bucketID, err)
		}

		p.saveSource(src.GetID(), file)
	default:
		return fmt.Errorf("unknown source type '%s'", src.GetType())
	}

	return nil
}

func (p *SourceProvider) saveSource(sourceID source.ID, file string) {
	p.mu.Lock()
	defer p.mu.Unlock()

	p.srcs[sourceID] = file
}

func (p *SourceProvider) RemoveSource(ctx context.Context, src sources.Source) {
	p.mu.Lock()
	defer p.mu.Unlock()

	delete(p.srcs, src.GetID())
}

func (p *SourceProvider) getSourceFile(ctx context.Context, sourceID source.ID) (string, bool) {
	p.mu.Lock()
	defer p.mu.Unlock()

	file, ok := p.srcs[sourceID]
	return file, ok
}

func (p *SourceProvider) Locate(ctx context.Context, sourceID source.ID) (path string, unlock func(), err error) {
	file, ok := p.getSourceFile(ctx, sourceID)
	if !ok {
		err = fmt.Errorf("source %s not found", sourceID.String())
		return
	}

	var bucketID bucket.ID
	if err = bucketID.FromString(sourceID.String()); err != nil {
		err = fmt.Errorf("failed to calculate bucket id: %w", err)
		return
	}

	return p.filestorage.LocateFile(ctx, bucketID, file)
}

func (p *SourceProvider) Read(ctx context.Context, sourceID source.ID) (r io.Reader, unlock func(), err error) {
	file, ok := p.getSourceFile(ctx, sourceID)
	if !ok {
		err = fmt.Errorf("source %s not found", sourceID.String())
		return
	}

	var bucketID bucket.ID
	if err = bucketID.FromString(sourceID.String()); err != nil {
		err = fmt.Errorf("failed to calculate bucket id: %w", err)
		return
	}

	return p.filestorage.ReadFile(ctx, bucketID, file)
}
