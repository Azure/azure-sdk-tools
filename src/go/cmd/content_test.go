// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"testing"

	"github.com/stretchr/testify/require"
)

func TestReceiverTypeName(t *testing.T) {
	tests := []struct {
		input, expectedName string
	}{
		{"Foo", "Foo"},
		{"*Foo", "Foo"},
		{"*Foo[T]", "Foo"},
		{"Foo[T, U]", "Foo"},
		{"(*Foo)", "Foo"},
		{"*(Foo)", "Foo"},
		{"* /* comment */ Foo", "Foo"},
		{"*État", "État"},
		{"*état", "état"},
	}
	for _, test := range tests {
		t.Run(test.input, func(t *testing.T) {
			fn := Func{ReceiverType: test.input}
			require.Equal(t, test.expectedName, fn.receiverTypeName())
		})
	}
}
