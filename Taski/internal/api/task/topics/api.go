package topics

import (
	"taski/internal/api"
)

type TaskTopicsListResponse struct {
	api.Response
	Topics []string `json:"topics,omitempty"`
}
