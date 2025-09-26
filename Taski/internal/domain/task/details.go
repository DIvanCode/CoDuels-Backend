package task

type Details struct {
	ID        ID     `json:"id"`
	Title     string `json:"title"`
	Type      Type   `json:"type"`
	Statement string `json:"statement"`
}

func (d Details) GetID() ID {
	return d.ID
}

func (d Details) GetType() Type {
	return d.Type
}

func (d Details) GetTitle() string {
	return d.Title
}

func (d Details) GetStatement() string {
	return d.Statement
}
