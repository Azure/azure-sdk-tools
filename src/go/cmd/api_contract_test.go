// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"os"
	"path/filepath"
	"strings"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestInterfaceTypeParameters(t *testing.T) {
	dir := t.TempDir()
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/contract\n\ngo 1.21\n"), 0644))
	for _, constraint := range []string{"any", "comparable"} {
		source := "package contract\ntype Handler[T " + constraint + ", U ~string] interface { Handle(T) U }\n"
		require.NoError(t, os.WriteFile(filepath.Join(dir, "types.go"), []byte(source), 0644))
		review, err := createReview(dir)
		require.NoError(t, err)
		line := feedbackLine(t, review, "contract.Handler")
		require.Equal(t, "typeHandler[T"+constraint+",U~string]interface", feedbackLineText(line))
		require.True(t, searchTokens(line.Children, func(token ReviewToken) bool { return token.Value == "Handle" }))
	}
}

func TestExportedFunctionNames(t *testing.T) {
	dir := t.TempDir()
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/contract\n\ngo 1.21\n"), 0644))
	source := "package contract\nfunc StartTestProxy() {}\nfunc StopTestProxy() {}\nfunc ExampleClient() {}\nfunc hiddenTest() {}\n"
	require.NoError(t, os.WriteFile(filepath.Join(dir, "functions.go"), []byte(source), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dir, "functions_test.go"), []byte("package contract\nfunc TestOnly() {}\n"), 0644))
	review, err := createReview(dir)
	require.NoError(t, err)
	for _, name := range []string{"StartTestProxy", "StopTestProxy", "ExampleClient"} {
		require.True(t, searchTokens(review.ReviewLines, func(token ReviewToken) bool { return token.Value == name }), "missing exported function %s", name)
	}
	for _, name := range []string{"hiddenTest", "TestOnly"} {
		require.False(t, searchTokens(review.ReviewLines, func(token ReviewToken) bool { return token.Value == name }))
	}
}

func TestAliasTypeIdentity(t *testing.T) {
	for _, test := range []struct{ name, definition string }{
		{"slice", "[]byte"},
		{"map", "map[string]int"},
		{"struct", "struct{ Value string }"},
		{"interface", "interface{ Method() }"},
	} {
		t.Run(test.name, func(t *testing.T) {
			dir := t.TempDir()
			require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/contract\n\ngo 1.21\n"), 0644))
			for _, imported := range []bool{false, true} {
				underlying := test.definition
				header := "package contract\n"
				if imported {
					internal := filepath.Join(dir, "internal", "types")
					require.NoError(t, os.MkdirAll(internal, 0755))
					require.NoError(t, os.WriteFile(filepath.Join(internal, "types.go"), []byte("package types\ntype Source "+test.definition+"\n"), 0644))
					header += "import \"example.com/contract/internal/types\"\n"
					underlying = "types.Source"
				}
				source := header + "type Alias = " + underlying + "\ntype Defined " + underlying + "\n"
				require.NoError(t, os.WriteFile(filepath.Join(dir, "types.go"), []byte(source), 0644))
				review, err := createReview(dir)
				require.NoError(t, err)
				alias := feedbackLineText(feedbackLine(t, review, "contract.Alias"))
				defined := feedbackLineText(feedbackLine(t, review, "contract.Defined"))
				require.Contains(t, alias, "typeAlias=")
				require.NotContains(t, defined, "=")
				require.Equal(t, strings.Replace(alias, "Alias=", "Defined", 1), defined)
			}
		})
	}
}
