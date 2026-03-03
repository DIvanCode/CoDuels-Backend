package config

import (
	flog "log"
	"os"
	"time"

	"github.com/ilyakaznacheev/cleanenv"
)

type (
	WorkerConfig struct {
		Env            string               `yaml:"env" env:"EXESH_ENV"`
		HttpServer     HttpServerConfig     `yaml:"http_server" env-prefix:"EXESH_HTTP_SERVER_"`
		FileStorage    FileStorageConfig    `yaml:"filestorage" env-prefix:"EXESH_FILE_STORAGE_"`
		SourceProvider SourceProviderConfig `yaml:"source_provider" env-prefix:"EXESH_SOURCE_PROVIDER_"`
		OutputProvider OutputProviderConfig `yaml:"output_provider" env-prefix:"EXESH_OUTPUT_PROVIDER_"`
		Worker         WorkConfig           `yaml:"worker" env-prefix:"EXESH_WORKER_"`
	}

	SourceProviderConfig struct {
		FilestorageBucketTTL time.Duration `yaml:"filestorage_bucket_ttl" env:"FILESTORAGE_BUCKET_TTL"`
		ArtifactTTL          time.Duration `yaml:"artifact_ttl" env:"ARTIFACT_TTL"`
	}

	OutputProviderConfig struct {
		ArtifactTTL time.Duration `yaml:"artifact_ttl" env:"ARTIFACT_TTL"`
	}

	WorkConfig struct {
		WorkerID            string        `yaml:"id" env:"ID"`
		FreeSlots           int           `yaml:"free_slots" env:"FREE_SLOTS"`
		CoordinatorEndpoint string        `yaml:"coordinator_endpoint" env:"COORDINATOR_ENDPOINT"`
		HeartbeatDelay      time.Duration `yaml:"heartbeat_delay" env:"HEARTBEAT_DELAY"`
		WorkerDelay         time.Duration `yaml:"worker_delay" env:"WORKER_DELAY"`
	}
)

func MustLoadWorkerConfig() (cfg *WorkerConfig) {
	configPath := os.Getenv("CONFIG_PATH")
	if configPath == "" {
		flog.Fatal("CONFIG_PATH is not set")
	}

	if _, err := os.Stat(configPath); os.IsNotExist(err) {
		flog.Fatalf("config file does not exist: %s", configPath)
	}

	cfg = &WorkerConfig{}
	if err := cleanenv.ReadConfig(configPath, cfg); err != nil {
		flog.Fatalf("cannot read config: %v", err)
	}

	return
}
