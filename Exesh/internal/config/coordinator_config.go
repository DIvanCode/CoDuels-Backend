package config

import (
	flog "log"
	"os"
	"time"

	"github.com/ilyakaznacheev/cleanenv"
)

type (
	CoordinatorConfig struct {
		Env        string           `yaml:"env"`
		HttpServer HttpServerConfig `yaml:"http_server"`
		Storage    StorageConfig    `yaml:"storage"`
	}

	StorageConfig struct {
		ConnectionString string        `yaml:"connection_string"`
		InitTimeout      time.Duration `yaml:"init_timeout"`
	}
)

func MustLoadCoordinatorConfig() (cfg *CoordinatorConfig) {
	configPath := os.Getenv("CONFIG_PATH")
	if configPath == "" {
		flog.Fatal("CONFIG_PATH is not set")
	}

	if _, err := os.Stat(configPath); os.IsNotExist(err) {
		flog.Fatalf("config file does not exist: %s", configPath)
	}

	cfg = &CoordinatorConfig{}
	if err := cleanenv.ReadConfig(configPath, cfg); err != nil {
		flog.Fatalf("cannot read config: %v", err)
	}

	return
}
