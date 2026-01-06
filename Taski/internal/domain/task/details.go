package task

type Details struct {
	ID        ID       `json:"id"`
	Title     string   `json:"title"`
	Type      Type     `json:"type"`
	Level     Level    `json:"level"`
	Topics    []string `json:"topics"`
	Statement string   `json:"statement"`
	Tests     []Test   `json:"tests"`
}

func (d Details) GetID() ID {
	return d.ID
}

func (d Details) GetTitle() string {
	return d.Title
}

func (d Details) GetType() Type {
	return d.Type
}

func (d Details) GetLevel() Level {
	return d.Level
}

func (d Details) GetTopics() []string {
	return d.Topics
}

func (d Details) GetStatement() string {
	return d.Statement
}

func (d Details) GetTests() []Test {
	return d.Tests
}
