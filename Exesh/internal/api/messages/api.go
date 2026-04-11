package messages

import (
	"exesh/internal/api"
	"exesh/internal/domain/execution/message/history"
)

type Response struct {
	api.Response
	Messages []history.Message `json:"messages,omitempty"`
}
