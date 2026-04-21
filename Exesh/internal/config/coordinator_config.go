package config

import (
	flog "log"
	"os"
	"time"

	"github.com/ilyakaznacheev/cleanenv"
)

type (
	CoordinatorConfig struct {
		Env                string                   `yaml:"env" env:"EXESH_ENV"`
		HttpServer         HttpServerConfig         `yaml:"http_server" env-prefix:"EXESH_HTTP_SERVER_"`
		Storage            StorageConfig            `yaml:"storage" env-prefix:"EXESH_STORAGE_"`
		FileStorage        FileStorageConfig        `yaml:"filestorage" env-prefix:"EXESH_FILE_STORAGE_"`
		JobFactory         JobFactoryConfig         `yaml:"job_factory" env-prefix:"EXESH_JOB_FACTORY_"`
		ExecutionScheduler ExecutionSchedulerConfig `yaml:"execution_scheduler" env-prefix:"EXESH_EXECUTION_SCHEDULER_"`
		WorkerPool         WorkerPoolConfig         `yaml:"worker_pool" env-prefix:"EXESH_WORKER_POOL_"`
		ArtifactRegistry   ArtifactRegistryConfig   `yaml:"artifact_registry" env-prefix:"EXESH_ARTIFACT_REGISTRY_"`
		Dispatcher         DispatcherConfig         `yaml:"dispatcher" env-prefix:"EXESH_DISPATCHER_"`
	}

	StorageConfig struct {
		ConnectionString string        `yaml:"connection_string" env:"CONNECTION_STRING"`
		InitTimeout      time.Duration `yaml:"init_timeout" env:"INIT_TIMEOUT"`
	}

	ExecutionSchedulerConfig struct {
		ExecutionsInterval  time.Duration `yaml:"executions_interval" env:"EXECUTIONS_INTERVAL"`
		Capacity            int64         `yaml:"capacity" env:"CAPACITY"`
		ExecutionRetryAfter time.Duration `yaml:"execution_retry_after" env:"EXECUTION_RETRY_AFTER"`
	}

	JobFactoryConfig struct {
		Output struct {
			CompiledBinary string `yaml:"compiled_binary" env:"COMPILED_BINARY"`
			RunOutput      string `yaml:"run_output" env:"RUN_OUTPUT"`
		} `yaml:"output" env-prefix:"OUTPUT_"`
		SourceTTL struct {
			FilestorageBucket time.Duration `yaml:"filestorage_bucket" env:"FILESTORAGE_BUCKET"`
		} `yaml:"source_ttl" env-prefix:"SOURCE_TTL_"`
		FilestorageEndpoint string `yaml:"filestorage_endpoint" env:"FILESTORAGE_ENDPOINT"`
	}

	WorkerPoolConfig struct {
		WorkerDieAfter time.Duration `yaml:"worker_die_after" env:"WORKER_DIE_AFTER"`
	}

	ArtifactRegistryConfig struct {
		ArtifactTTL time.Duration `yaml:"artifact_ttl" env:"ARTIFACT_TTL"`
	}

	DispatcherConfig struct {
		KafkaEnabled bool     `yaml:"kafka_enabled" env:"KAFKA_ENABLED"`
		Brokers      []string `yaml:"brokers" env:"BROKERS" env-separator:","`
		Topic        string   `yaml:"topic" env:"TOPIC"`
		SaslAuth     bool     `yaml:"sasl_auth" env:"SASL_AUTH"`
		SaslUsername string   `yaml:"sasl_username" env:"SASL_USERNAME"`
		SaslPassword string   `yaml:"sasl_password" env:"SASL_PASSWORD"`
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
