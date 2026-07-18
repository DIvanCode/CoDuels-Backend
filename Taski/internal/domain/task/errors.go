package task

import "errors"

var (
	ErrNotFound     = errors.New("task not found")
	ErrFileNotFound = errors.New("task file not found")
)
