package config

import (
	flog "log"
	"os"
	"time"

	"github.com/ilyakaznacheev/cleanenv"
)

type (
	CoordinatorConfig struct {
		Env                string                   `yaml:"env" env:"ENV"`
		HttpServer         HttpServerConfig         `yaml:"http_server" env-prefix:"HTTP_SERVER_"`
		Storage            StorageConfig            `yaml:"storage" env-prefix:"STORAGE_"`
		FileStorage        FileStorageConfig        `yaml:"filestorage" env-prefix:"FILE_STORAGE_"`
		JobFactory         JobFactoryConfig         `yaml:"job_factory" env-prefix:"JOB_FACTORY_"`
		ExecutionScheduler ExecutionSchedulerConfig `yaml:"execution_scheduler" env-prefix:"EXECUTION_SCHEDULER_"`
		JobScheduler       JobSchedulerConfig       `yaml:"job_scheduler" env-prefix:"JOB_SCHEDULER_"`
		WorkerPool         WorkerPoolConfig         `yaml:"worker_pool" env-prefix:"WORKER_POOL_"`
		Dispatcher         DispatcherConfig         `yaml:"dispatcher" env-prefix:"DISPATCHER_"`
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

	JobSchedulerConfig struct {
		PromisedJobsLimit int `yaml:"promised_jobs_limit" env:"PROMISED_JOBS_LIMIT"`
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
