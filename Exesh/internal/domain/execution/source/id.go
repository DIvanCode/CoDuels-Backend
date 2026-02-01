package source

import (
	"crypto/sha1"
	"encoding/json"
	"fmt"
)

type ID [2 * sha1.Size]byte

func (id *ID) String() string {
	return string(id[:])
}

func (id *ID) FromString(s string) error {
	if len(s) != len(id) {
		return fmt.Errorf("invalid hex string length")
	}
	for _, c := range s {
		if '0' <= c && c <= '9' {
			continue
		}
		if 'a' <= c && c <= 'f' {
			continue
		}
		return fmt.Errorf("invalid hex string char: %c", c)
	}
	copy(id[:], s)
	return nil
}

func (id *ID) MarshalJSON() ([]byte, error) {
	return json.Marshal(id.String())
}

func (id *ID) UnmarshalJSON(data []byte) error {
	var s string
	if err := json.Unmarshal(data, &s); err != nil {
		return fmt.Errorf("id should be a string, got %s", data)
	}
	return id.FromString(s)
}
