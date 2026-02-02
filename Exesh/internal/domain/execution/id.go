package execution

import (
	"database/sql/driver"
	"encoding/json"
	"fmt"

	"github.com/google/uuid"
)

type ID uuid.UUID

func newID() ID {
	return ID(uuid.New())
}

func (id *ID) String() string {
	uid := uuid.UUID(*id)
	return uid.String()
}

func (id *ID) FromString(idStr string) (err error) {
	var uid uuid.UUID
	if uid, err = uuid.Parse(idStr); err != nil {
		return
	}
	*id = ID(uid)
	return
}

func (id *ID) MarshalJSON() ([]byte, error) {
	return json.Marshal(id.String())
}

func (id *ID) UnmarshalJSON(data []byte) error {
	var uid uuid.UUID
	err := json.Unmarshal(data, &uid)
	if err != nil {
		return err
	}
	*id = ID(uid)
	return nil
}

func (id ID) Value() (driver.Value, error) {
	return id.String(), nil
}

func (id *ID) Scan(src any) error {
	switch v := src.(type) {
	case string:
		return id.FromString(v)
	case []byte:
		return id.FromString(string(v))
	case nil:
		return nil
	default:
		return fmt.Errorf("failed to scan execution id from type %T", src)
	}
}
