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
		{"(f (*Foo))", "f", "(*Foo)"},
		{"(f *(Foo))", "f", "*(Foo)"},
		{"((*Foo))", "", "(*Foo)"},
		{"(f *\u00c9tat)", "f", "*\u00c9tat"},
	}
	for _, test := range tests {
		t.Run(test.input, func(t *testing.T) {
			name, typ := getReceiver(test.input)
			require.Equal(t, test.expectedName, name)
			require.Equal(t, test.expectedType, typ)
		})
	}
}

func TestIsOnUnexportedMember(t *testing.T) {
	for _, test := range []struct {
		signature string
		private   bool
	}{
		{"(hidden) Method", true},
		{"(*hidden) Method", true},
		{"(h *hidden[T]) Method", true},
		{"(h (*hidden)) Method", true},
		{"(h *\u00e9tat) Method", true},
		{"(Public) Method", false},
		{"(p *Public[T]) Method", false},
		{"(p *(Public)) Method", false},
		{"(p *\u00c9tat) Method", false},
		{"Function", false},
	} {
		t.Run(test.signature, func(t *testing.T) {
			require.Equal(t, test.private, isOnUnexportedMember(test.signature))
		})
	}
}
