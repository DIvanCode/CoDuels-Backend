package task

type Task interface {
	GetID() ID
	GetTitle() string
	GetType() Type
	GetLevel() Level
	GetTopics() []string
	GetStatement() string
	GetTests() []Test
}
