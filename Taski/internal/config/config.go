package config

import (
	flog "log"
	"os"
	"time"

	filestorage "github.com/DIvanCode/filestorage/pkg/config"
	"github.com/ilyakaznacheev/cleanenv"
)

type (
	Config struct {
		Env              string                 `yaml:"env" env:"ENV"`
		HttpServer       HttpServerConfig       `yaml:"http_server" env-prefix:"HTTP_SERVER_"`
		FileStorage      filestorage.Config     `yaml:"filestorage" env-prefix:"FILESTORAGE_"`
		Db               DbConfig               `yaml:"db" env-prefix:"DB_"`
		Execute          ExecuteConfig          `yaml:"execute" env-prefix:"EXECUTE_"`
		EventConsumer    EventConsumerConfig    `yaml:"event_consumer" env-prefix:"EVENT_CONSUMER_"`
		MessageProducer  MessageProducerConfig  `yaml:"message_producer" env-prefix:"MESSAGE_PRODUCER_"`
		MetricsCollector MetricsCollectorConfig `yaml:"metrics_collector" env-prefix:"METRICS_COLLECTOR_"`
		Tasks            TasksList              `yaml:"tasks" env:"TASKS" env-separator:","`
		TaskTopics       TaskTopicsList         `yaml:"task_topics" env:"TASK_TOPICS" env-separator:","`
	}

	HttpServerConfig struct {
		Addr        string `yaml:"addr" env:"ADDR"`
		MetricsAddr string `yaml:"metrics_addr" env:"METRICS_ADDR"`
	}

	DbConfig struct {
		ConnectionString string        `yaml:"connection_string" env:"CONNECTION_STRING"`
		InitTimeout      time.Duration `yaml:"init_timeout" env:"INIT_TIMEOUT"`
	}

	ExecuteConfig struct {
		Endpoint             string `yaml:"endpoint" env:"ENDPOINT"`
		DownloadTaskEndpoint string `yaml:"download_task_endpoint" env:"DOWNLOAD_TASK_ENDPOINT"`
	}

	EventConsumerConfig struct {
		Brokers       []string      `yaml:"brokers" env:"BROKERS" env-separator:","`
		Topic         string        `yaml:"topic" env:"TOPIC"`
		GroupID       string        `yaml:"group_id" env:"GROUP_ID"`
		FetchInterval time.Duration `yaml:"fetch_interval" env:"FETCH_INTERVAL"`
	}

	MessageProducerConfig struct {
		Brokers []string `yaml:"brokers" env:"BROKERS" env-separator:","`
		Topic   string   `yaml:"topic" env:"TOPIC"`
	}

	MetricsCollectorConfig struct {
		CollectInterval time.Duration `yaml:"collect_interval" env:"COLLECT_INTERVAL"`
	}

	TasksList []string

	TaskTopicsList []string
)

func MustLoad() (cfg *Config) {
	configPath := os.Getenv("CONFIG_PATH")
	if configPath == "" {
		configPath = "config/config.yml"
	}

	if _, err := os.Stat(configPath); os.IsNotExist(err) {
		flog.Fatalf("config file does not exist: %s", configPath)
	}

	cfg = &Config{}
	if err := cleanenv.ReadConfig(configPath, cfg); err != nil {
		flog.Fatalf("cannot read config: %v", err)
	}

	if err := cleanenv.ReadEnv(cfg); err != nil {
		flog.Fatalf("cannot read env: %v", err)
	}

	return
}
