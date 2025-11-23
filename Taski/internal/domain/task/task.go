package task

type Task interface {
	GetID() ID
	GetType() Type
	GetLevel() Level
	GetTitle() string
	GetStatement() string
	GetTests() []Test
}
