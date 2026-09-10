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
			var cfg Config
			if err := cleanenv.ReadConfig("../../config/taski.yml", &cfg); err != nil {
				t.Fatal(err)
			}
			expected := key
			if expected == "" {
				expected = "INTERNAL_AUTH_KEY_LOCAL_VALUE"
			}
			if cfg.InternalAuthKey != expected {
				t.Error("incorrect internal auth key")
			}
		})
	}
}
