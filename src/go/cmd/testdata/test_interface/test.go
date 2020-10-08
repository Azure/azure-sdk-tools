// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package testinterface

type EmbeddedInt interface {
	Bar(b string)
}

type SomeInterface interface {
	EmbeddedInt
	Foo(bar int64) *bool
}

type doNotShowInterface struct {
}

func (d *doNotShowInterface) Foo(bar string) *bool {
	return nil
}
