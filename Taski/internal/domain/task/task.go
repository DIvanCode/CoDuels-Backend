package task

type Task interface {
	GetID() ID
	GetType() Type
	GetTitle() string
	GetStatement() string
}
