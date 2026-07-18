package safepath

import (
	"errors"
	"path"
	"strings"
	"unicode"
)

var ErrInvalidPath = errors.New("invalid relative path")

// Clean validates an untrusted relative path and returns its canonical,
// slash-separated representation. Both slash styles are treated as separators
// so the result stays safe if a task package crosses operating systems.
func Clean(name string) (string, error) {
	if name == "" || strings.IndexByte(name, 0) >= 0 {
		return "", ErrInvalidPath
	}

	portable := strings.ReplaceAll(name, `\`, "/")
	if strings.HasPrefix(portable, "/") || hasWindowsVolume(portable) {
		return "", ErrInvalidPath
	}

	for _, part := range strings.Split(portable, "/") {
		if part == "" || part == "." || part == ".." {
			return "", ErrInvalidPath
		}
	}

	clean := path.Clean(portable)
	if clean == "." || clean == ".." || strings.HasPrefix(clean, "../") {
		return "", ErrInvalidPath
	}

	return clean, nil
}

func hasWindowsVolume(name string) bool {
	return len(name) >= 2 && unicode.IsLetter(rune(name[0])) && name[1] == ':'
}
