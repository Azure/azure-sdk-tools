// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package testconst

type SomeChoice int64

const (
	Choice1 SomeChoice = 1
	Choice2 SomeChoice = 2
)

const (
	SomeConst string = "somestring"
)

const (
	Agent   = "foo/" + Version
	Version = "0.1.0"
)
