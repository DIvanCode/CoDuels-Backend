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
		Env                string                   `yaml:"env"`
		HttpServer         HttpServerConfig         `yaml:"http_server"`
		Storage            StorageConfig            `yaml:"storage"`
		FileStorage        filestorage.Config       `yaml:"filestorage"`
		InputProvider      InputProviderConfig      `yaml:"input_provider"`
		JobFactory         JobFactoryConfig         `yaml:"job_factory"`
		ExecutionScheduler ExecutionSchedulerConfig `yaml:"execution_scheduler"`
		WorkerPool         WorkerPoolConfig         `yaml:"worker_pool"`
		ArtifactRegistry   ArtifactRegistryConfig   `yaml:"artifact_registry"`
		Sender             SenderConfig             `yaml:"sender"`
	}

	StorageConfig struct {
		ConnectionString string        `yaml:"connection_string"`
		InitTimeout      time.Duration `yaml:"init_timeout"`
	}

	ExecutionSchedulerConfig struct {
		ExecutionsInterval  time.Duration `yaml:"executions_interval"`
		MaxConcurrency      int           `yaml:"max_concurrency"`
		ExecutionRetryAfter time.Duration `yaml:"execution_retry_after"`
	}

	JobFactoryConfig struct {
		Output struct {
			CompiledCpp  string `yaml:"compiled_cpp"`
			RunOutput    string `yaml:"run_output"`
			CheckVerdict string `yaml:"check_verdict"`
		} `yaml:"output"`
	}

	WorkerPoolConfig struct {
		WorkerDieAfter time.Duration `yaml:"worker_die_after"`
	}

	ArtifactRegistryConfig struct {
		ArtifactTTL time.Duration `yaml:"artifact_ttl"`
	}

	SenderConfig struct {
		Brokers []string `yaml:"brokers"`
		Topic   string   `yaml:"topic"`
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
