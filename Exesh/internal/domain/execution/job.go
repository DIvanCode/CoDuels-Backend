package execution

import (
	"crypto/sha1"
	"encoding/json"
	"fmt"
)

type (
	Job interface {
		GetID() JobID
		GetType() JobType
		GetInputs() []Input
		GetOutput() Output
	}

	JobDetails struct {
		ID   JobID   `json:"id"`
		Type JobType `json:"type"`
	}

	JobID [2 * sha1.Size]byte

	JobType string
)

const (
	CompileCppJobType JobType = "compile_cpp"
	RunCppJobType     JobType = "run_cpp"
	RunPyJobType      JobType = "run_py"
	RunGoJobType      JobType = "run_go"
	CheckCppJobType   JobType = "check_cpp"
)

func (job JobDetails) GetID() JobID {
	return job.ID
}

func (job JobDetails) GetType() JobType {
	return job.Type
}

func (id JobID) String() string {
	return string(id[:])
}

func (id *JobID) FromString(s string) error {
	if len(s) != len(id) {
		return fmt.Errorf("invalid hex string length")
	}
	for _, c := range s {
		if '0' <= c && c <= '9' {
			continue
		}
		if 'a' <= c && c <= 'f' {
			continue
		}
		return fmt.Errorf("invalid hex string char: %c", c)
	}
	copy(id[:], s)
	return nil
}

func (id JobID) MarshalJSON() ([]byte, error) {
	return json.Marshal(id.String())
}

func (id *JobID) UnmarshalJSON(data []byte) error {
	var s string
	if err := json.Unmarshal(data, &s); err != nil {
		return fmt.Errorf("id should be a string, got %s", data)
	}
	return id.FromString(s)
}
