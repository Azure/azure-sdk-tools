package teststruct

import "net/http"

type SomeStruct struct {
	Foo  *bool
	Resp *http.Response
}

func NewStruct() *SomeStruct {
	return nil
}

func (s *SomeStruct) Update(resp *http.Response) error {
	return nil
}
