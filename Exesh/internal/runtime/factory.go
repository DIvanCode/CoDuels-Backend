package runtime

import "context"

type RuntimeFactory interface {
	Create(context.Context) (Runtime, error)
}
