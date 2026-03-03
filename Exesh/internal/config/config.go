package config

type (
	HttpServerConfig struct {
		Addr        string `yaml:"addr" env:"ADDR"`
		MetricsAddr string `yaml:"metrics_addr" env:"METRICS_ADDR"`
	}
)
