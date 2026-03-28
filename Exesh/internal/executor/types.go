package executor

import "exesh/internal/domain/execution/source"

type PreparedInput struct {
	Paths map[source.ID]string
}

type PreparedOutput struct {
	Path          string
	AlreadyExists bool
}
