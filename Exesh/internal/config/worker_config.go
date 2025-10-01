package config

import (
	flog "log"
	"os"
	"time"

	filestorage "github.com/DIvanCode/filestorage/pkg/config"
	"github.com/ilyakaznacheev/cleanenv"
)

type (
	WorkerConfig struct {
		Env            string               `yaml:"env"`
		HttpServer     HttpServerConfig     `yaml:"http_server"`
		FileStorage    filestorage.Config   `yaml:"filestorage"`
		InputProvider  InputProviderConfig  `yaml:"input_provider"`
		OutputProvider OutputProviderConfig `yaml:"output_provider"`
	}

	OutputProviderConfig struct {
		ArtifactTTL time.Duration `yaml:"artifact_ttl"`
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
