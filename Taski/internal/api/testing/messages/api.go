package messages

import (
	"taski/internal/api"
	"taski/internal/domain/testing/message/history"
)

type Response struct {
	api.Response
	Messages []history.Message `json:"messages,omitempty"`
}
