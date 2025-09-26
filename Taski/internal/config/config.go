package config

import (
	flog "log"
	"os"

	filestorage "github.com/DIvanCode/filestorage/pkg/config"
	"github.com/ilyakaznacheev/cleanenv"
)

type (
	Config struct {
		Env         string             `yaml:"env"`
		HttpServer  HttpServerConfig   `yaml:"http_server"`
		FileStorage filestorage.Config `yaml:"filestorage"`
	}

	HttpServerConfig struct {
		Addr string `yaml:"addr"`
	}
)

func MustLoad() (cfg *Config) {
	configPath := os.Getenv("CONFIG_PATH")
	if configPath == "" {
		flog.Fatal("CONFIG_PATH is not set")
	}

	if _, err := os.Stat(configPath); os.IsNotExist(err) {
		flog.Fatalf("config file does not exist: %s", configPath)
	}

	cfg = &Config{}
	if err := cleanenv.ReadConfig(configPath, cfg); err != nil {
		flog.Fatalf("cannot read config: %v", err)
	}

	return
}
