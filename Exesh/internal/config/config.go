package config

type (
	HttpServerConfig struct {
		Addr        string `yaml:"addr"`
		MetricsAddr string `yaml:"metrics_addr"`
	}
)
