// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package testvars

import "net/http"

var Client *http.Client

type SomeChoice struct{}

var (
	SomeChoice1 *SomeChoice = &SomeChoice{}
	SomeChoice2 *SomeChoice = &SomeChoice{}
)
