package config

import filestorage "github.com/DIvanCode/filestorage/pkg/config"

type (
	FileStorageConfig struct {
		RootDir string                   `yaml:"root_dir" env:"ROOT_DIR"`
		Trasher FileStorageTrasherConfig `yaml:"trasher" env-prefix:"TRASHER_"`
	}

	FileStorageTrasherConfig struct {
		Workers                  int `yaml:"workers" env:"WORKERS"`
		CollectorIterationsDelay int `yaml:"collector_iterations_delay" env:"COLLECTOR_ITERATIONS_DELAY"`
		WorkerIterationsDelay    int `yaml:"worker_iterations_delay" env:"WORKER_ITERATIONS_DELAY"`
	}
)

func (c FileStorageConfig) ToExternal() filestorage.Config {
	return filestorage.Config{
		RootDir: c.RootDir,
		Trasher: filestorage.TrasherConfig{
			Workers:                  c.Trasher.Workers,
			CollectorIterationsDelay: c.Trasher.CollectorIterationsDelay,
			WorkerIterationsDelay:    c.Trasher.WorkerIterationsDelay,
		},
	}
}
