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
		Env              string                 `yaml:"env"`
		HttpServer       HttpServerConfig       `yaml:"http_server"`
		FileStorage      filestorage.Config     `yaml:"filestorage"`
		Db               DbConfig               `yaml:"db"`
		Execute          ExecuteConfig          `yaml:"execute"`
		EventConsumer    EventConsumerConfig    `yaml:"event_consumer"`
		MessageProducer  MessageProducerConfig  `yaml:"message_producer"`
		MetricsCollector MetricsCollectorConfig `yaml:"metrics_collector"`
		Tasks            TasksList              `yaml:"tasks"`
		TaskTopics       TaskTopicsList         `yaml:"task_topics"`
	}

	HttpServerConfig struct {
		Addr        string `yaml:"addr"`
		MetricsAddr string `yaml:"metrics_addr"`
	}

	DbConfig struct {
		ConnectionString string        `yaml:"connection_string"`
		InitTimeout      time.Duration `yaml:"init_timeout"`
	}

	ExecuteConfig struct {
		Endpoint             string `yaml:"endpoint"`
		DownloadTaskEndpoint string `yaml:"download_task_endpoint"`
	}

	EventConsumerConfig struct {
		Brokers       []string      `yaml:"brokers"`
		Topic         string        `yaml:"topic"`
		GroupID       string        `yaml:"group_id"`
		FetchInterval time.Duration `yaml:"fetch_interval"`
	}

	MessageProducerConfig struct {
		Brokers []string `yaml:"brokers"`
		Topic   string   `yaml:"topic"`
	}

	MetricsCollectorConfig struct {
		CollectInterval time.Duration `yaml:"collect_interval"`
	}

	TasksList []string

	TaskTopicsList []string
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
