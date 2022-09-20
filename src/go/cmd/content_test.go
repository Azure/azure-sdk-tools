// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"testing"

	"github.com/stretchr/testify/require"
)

func TestGetReceiver(t *testing.T) {
	tests := []struct {
		input, expectedName, expectedType string
	}{
		{"(Foo)", "", "Foo"},
		{"(*Foo)", "", "*Foo"},
		{"(*Foo[T])", "", "*Foo[T]"},
		{"(*Foo[T, U])", "", "*Foo[T, U]"},
		{"(f Foo)", "f", "Foo"},
		{"(f *Foo)", "f", "*Foo"},
		{"(f Foo[T])", "f", "Foo[T]"},
		{"(f *Foo[T])", "f", "*Foo[T]"},
		{"(f Foo[T, U])", "f", "Foo[T, U]"},
		{"(f *Foo[T, U])", "f", "*Foo[T, U]"},
	}
	for _, test := range tests {
		t.Run(test.input, func(t *testing.T) {
			name, typ := getReceiver(test.input)
			require.Equal(t, test.expectedName, name)
			require.Equal(t, test.expectedType, typ)
		})
	}
}
