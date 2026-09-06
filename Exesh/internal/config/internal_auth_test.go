package config

import (
	"os"
	"testing"

	"github.com/ilyakaznacheev/cleanenv"
)

func TestInternalAuthKeyConfiguration(t *testing.T) {
	for _, key := range []string{"", "environment-test-key"} {
		t.Run(key, func(t *testing.T) {
			t.Setenv("INTERNAL_AUTH_KEY", key)
			if key == "" {
				if err := os.Unsetenv("INTERNAL_AUTH_KEY"); err != nil {
					t.Fatal(err)
				}
			}
			var worker WorkerConfig
			if err := cleanenv.ReadConfig("../../config/worker.yml", &worker); err != nil {
				t.Fatal(err)
			}
			var coordinator CoordinatorConfig
			if err := cleanenv.ReadConfig("../../config/coordinator.yml", &coordinator); err != nil {
				t.Fatal(err)
			}
			expected := key
			if expected == "" {
				expected = "INTERNAL_AUTH_KEY_LOCAL_VALUE"
			}
			if worker.InternalAuthKey != expected || coordinator.InternalAuthKey != expected {
				t.Error("incorrect internal auth key")
			}
		})
	}
}
