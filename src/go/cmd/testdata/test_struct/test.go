// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package teststruct

import "net/http"

type someStruct struct {
	Foo *string
	bar int
}

type SomeStruct struct {
	Foo  *bool
	Resp *http.Response
	t    string
}

func NewSomeStruct() *SomeStruct {
	return nil
}

func NewStruct() *SomeStruct {
	return nil
}

func (s *SomeStruct) Update(resp *http.Response) error {
	return nil
}
