package config

import (
	flog "log"
	"os"
	"time"

	filestorage "github.com/DIvanCode/filestorage/pkg/config"
	"github.com/ilyakaznacheev/cleanenv"
)

type (
	CoordinatorConfig struct {
		Env           string              `yaml:"env"`
		HttpServer    HttpServerConfig    `yaml:"http_server"`
		Storage       StorageConfig       `yaml:"storage"`
		FileStorage   filestorage.Config  `yaml:"filestorage"`
		InputProvider InputProviderConfig `yaml:"input_provider"`
		GraphFactory  GraphFactoryConfig  `yaml:"graph_factory"`
		Scheduler     SchedulerConfig     `yaml:"scheduler"`
	}

	StorageConfig struct {
		ConnectionString string        `yaml:"connection_string"`
		InitTimeout      time.Duration `yaml:"init_timeout"`
	}

	SchedulerConfig struct {
		ExecutionsInterval  time.Duration `yaml:"executions_interval"`
		MaxConcurrency      int           `yaml:"max_concurrency"`
		ExecutionRetryAfter time.Duration `yaml:"execution_retry_after"`
	}

	GraphFactoryConfig struct {
		Output struct {
			CompiledCpp  string `yaml:"compiled_cpp"`
			RunOutput    string `yaml:"run_output"`
			CheckVerdict string `yaml:"check_verdict"`
		} `yaml:"output"`
	}

	InputProviderConfig struct {
		FilestorageBucketTTL time.Duration `yaml:"filestorage_bucket_ttl"`
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
