package config

import "testing"

func TestFileStorageConfigPropagatesInternalAuthKey(t *testing.T) {
	config := FileStorageConfig{RootDir: "storage"}.ToExternal("test-key")

	if config.InternalAuthKey != "test-key" {
		t.Fatalf("got internal auth key %q, want %q", config.InternalAuthKey, "test-key")
	}
	if config.RootDir != "storage" {
		t.Fatalf("got root directory %q, want %q", config.RootDir, "storage")
	}
}
