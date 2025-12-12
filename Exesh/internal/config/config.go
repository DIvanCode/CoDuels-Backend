package config

import "time"

type (
	HttpServerConfig struct {
		Addr        string `yaml:"addr"`
		MetricsAddr string `yaml:"metrics_addr"`
	}

	InputProviderConfig struct {
		FilestorageBucketTTL time.Duration `yaml:"filestorage_bucket_ttl"`
		ArtifactTTL          time.Duration `yaml:"artifact_ttl"`
	}
)
