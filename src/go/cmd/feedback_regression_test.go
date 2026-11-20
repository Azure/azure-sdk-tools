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

func TestInternalAliasesOnlyFailWhenExposed(t *testing.T) {
	dir := aliasFeedbackFixture(t, "type Public int\n", `
import "example.com/aliastest/internal/other"
type Unused = other.Pointer
`, map[string]string{
		filepath.Join("internal", "other", "other.go"): "package other\ntype Pointer *int\n",
	})
	_, err := createReview(dir)
	require.NoError(t, err)
	require.NoError(t, os.WriteFile(filepath.Join(dir, "alias.go"), []byte(`
package aliastest
import "example.com/aliastest/internal/types"
type Public = types.Unused
`), 0644))
	_, err = createReview(dir)
	require.ErrorContains(t, err, "unsupported type *ast.StarExpr")
}

func TestUnresolvedInternalAliasFailsWhenExposed(t *testing.T) {
	dir := aliasFeedbackFixture(t, "type Public = types.External\n", `
import "example.com/elsewhere"
type External = elsewhere.Entry
`, nil)
	_, err := createReview(dir)
	require.ErrorContains(t, err, "couldn't find package example.com/elsewhere")
}

func TestUnsupportedHiddenCompositeTypes(t *testing.T) {
	for _, test := range []struct {
		name, aliases, definitions, expected string
	}{
		{"generic", "type Public = types.Concrete\n", "type Box[T any] struct{ Field T }\ntype Concrete = Box[int]\n", "unsupported type *ast.IndexExpr"},
		{"multi-argument generic", "type Public = types.Concrete\n", "type Box[T, U any] struct{}\ntype Concrete = Box[int,string]\n", "unsupported type *ast.IndexListExpr"},
		{"array constant", "type Public = types.Data\n", "const width = 16\ntype Data [width]byte\n", "array length expression"},
		{"nested array constant", "type Public = types.Data\n", "const width = 16\ntype Data [][width]byte\n", "array length expression"},
		{"slice component", "type Public = types.Data\n", "type Entry struct { Field int }\ntype Data []Entry\n", "hidden component type"},
		{"map component", "type Public = types.Data\n", "type Entry struct { Field int }\ntype Data map[string]*Entry\n", "hidden component type"},
		{"variadic component", "type Public = types.Data\n", "type Entry struct { Field int }\ntype Data func(...Entry)\n", "hidden component type"},
		{"defined pointer", "type Public *types.Data\n", "type Data struct { Field int }\n", "hidden component type"},
		{"private defined pointer", "type hidden = *types.Data\ntype Public hidden\n", "type Data struct { Field int }\n", "hidden component type"},
	} {
		t.Run(test.name, func(t *testing.T) {
			dir := aliasFeedbackFixture(t, test.aliases, test.definitions, nil)
			_, err := createReview(dir)
			require.ErrorContains(t, err, test.expected)
		})
	}
}

func TestUnsupportedDirectCompositeAndGenericTypes(t *testing.T) {
	for _, test := range []struct {
		name, source, expected string
	}{
		{"hidden slice component", "type hidden struct{}\ntype Public []hidden\n", "hidden component type"},
		{"generic instantiation alias", "type Box[T any] struct{}\ntype Public = Box[int]\n", "generic instantiation"},
	} {
		t.Run(test.name, func(t *testing.T) {
			dir := t.TempDir()
			require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/contract\n\ngo 1.21\n"), 0644))
			require.NoError(t, os.WriteFile(filepath.Join(dir, "types.go"), []byte("package contract\n"+test.source), 0644))
			_, err := createReview(dir)
			require.ErrorContains(t, err, test.expected)
		})
	}
}

func TestUnsupportedHiddenHoistedMethodTypes(t *testing.T) {
	for _, test := range []struct{ name, method string }{
		{"parameter", "func (Data) Method(value hidden) {}"},
		{"result", "func (Data) Method() hidden { return hidden{} }"},
	} {
		t.Run(test.name, func(t *testing.T) {
			dir := aliasFeedbackFixture(t, "type Public = types.Data\n", "type Data []byte\ntype hidden struct{}\n"+test.method+"\n", nil)
			_, err := createReview(dir)
			require.ErrorContains(t, err, "hidden component type")
		})
	}
}

func TestDefinedPointerToPrimitive(t *testing.T) {
	dir := aliasFeedbackFixture(t, "type Public types.Ptr\n", "type Ptr *int\n", nil)
	review, err := createReview(dir)
	require.NoError(t, err)
	require.Equal(t, "typePublic*int", feedbackLineText(feedbackLine(t, review, "aliastest.Public")))

	require.NoError(t, os.WriteFile(filepath.Join(dir, "alias.go"), []byte("package aliastest\ntype hidden = *int\ntype Public hidden\n"), 0644))
	review, err = createReview(dir)
	require.NoError(t, err)
	require.Equal(t, "typePublic*int", feedbackLineText(feedbackLine(t, review, "aliastest.Public")))
}

func TestCompositeSourceImportIdentity(t *testing.T) {
	dir := aliasFeedbackFixture(t, "type Public = types.Data\n", `
import model "example.com/aliastest/model"
type Data []model.Item
`, map[string]string{
		filepath.Join("model", "model.go"): "package model\ntype Item struct { Field int }\n",
		filepath.Join("other", "other.go"): "package other\ntype Item struct { Field string }\n",
	})
	review, err := createReview(dir)
	require.NoError(t, err)
	line := feedbackLine(t, review, "aliastest.Public")
	require.Contains(t, feedbackLineText(line), "example.com/aliastest/model.Item")
	require.True(t, searchTokens([]ReviewLine{line}, func(t ReviewToken) bool {
		return t.NavigateToID == "aliastest/model.Item"
	}))
	source := filepath.Join(dir, "internal", "types", "types.go")
	require.NoError(t, os.WriteFile(source, []byte(strings.Replace(`
package types
import model "example.com/aliastest/model"
type Data []model.Item
`, "/model\"", "/other\"", 1)), 0644))
	changed, err := createReview(dir)
	require.NoError(t, err)
	require.Contains(t, feedbackLineText(feedbackLine(t, changed, "aliastest.Public")), "example.com/aliastest/other.Item")
}

func TestCompositeTypeRejectsUnknownImportName(t *testing.T) {
	dir := aliasFeedbackFixture(t, "type Public = types.Data\n", `
import model "example.com/aliastest/model"
type Data []missing.Item
`, map[string]string{
		filepath.Join("model", "model.go"): "package model\ntype Item struct{}\n",
	})
	_, err := createReview(dir)
	require.ErrorContains(t, err, "unknown import name missing")
}

func TestPrivateReceiverAliasAndComments(t *testing.T) {
	dir := t.TempDir()
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/contract\n\ngo 1.21\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dir, "types.go"), []byte(`
package contract
type Public []byte
type hidden = Public
func (hidden) M() {}
func (r * /* comment */ Public) WithComment() {}
type private []byte
type Alias = private
func (p (*Alias)) Once() {}
`), 0644))
	review, err := createReview(dir)
	require.NoError(t, err)
	public := feedbackLine(t, review, "contract.Public")
	feedbackMethod(t, public, "Public", "M")
	feedbackMethod(t, public, "Public", "WithComment")
	alias := feedbackLine(t, review, "contract.Alias")
	count := 0
	for _, child := range alias.Children {
		if searchTokens([]ReviewLine{child}, func(token ReviewToken) bool { return token.Value == "Once" }) {
			count++
		}
	}
	require.Equal(t, 1, count)
}

func TestSelfReferenceDoesNotRenameAnonymousFields(t *testing.T) {
	dir := aliasFeedbackFixture(t, "type Public = types.Data\n", `
type Data []byte
func (Data) Example(value struct { Data int; Other Data }) struct { Data int; Other Data } { return value }
func (Data) Variadic(value ...struct { Data int; Other Data }) {}
`, nil)
	review, err := createReview(dir)
	require.NoError(t, err)
	method := feedbackMethod(t, feedbackLine(t, review, "aliastest.Public"), "Public", "Example")
	text := feedbackLineText(method)
	require.Equal(t, 2, strings.Count(text, "Dataint"))
	require.Equal(t, 2, strings.Count(text, "OtherPublic"))
	require.NotContains(t, text, "Publicint")
	variadic := feedbackLineText(feedbackMethod(t, feedbackLine(t, review, "aliastest.Public"), "Public", "Variadic"))
	require.Contains(t, variadic, "Dataint")
	require.Contains(t, variadic, "OtherPublic")
	require.NotContains(t, variadic, "Publicint")
}

func TestPrivateAliasToStandardLibraryType(t *testing.T) {
	dir := t.TempDir()
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/contract\n\ngo 1.21\n"), 0644))
	for _, target := range []string{"Duration", "Time"} {
		source := "package contract\nimport \"time\"\ntype hidden = time." + target + "\ntype Public = hidden\n"
		require.NoError(t, os.WriteFile(filepath.Join(dir, "types.go"), []byte(source), 0644))
		review, err := createReview(dir)
		require.NoError(t, err)
		require.Equal(t, "typePublic=time."+target, feedbackLineText(feedbackLine(t, review, "contract.Public")))
	}
}

func TestLocalAliasStructFieldDiagnostic(t *testing.T) {
	dir := t.TempDir()
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/contract\n\ngo 1.21\n"), 0644))
	source := "package contract\ntype RetryOptions struct{}\ntype options struct { Retry RetryOptions }\ntype Alias = options\ntype Defined options\n"
	require.NoError(t, os.WriteFile(filepath.Join(dir, "types.go"), []byte(source), 0644))
	review, err := createReview(dir)
	require.NoError(t, err)
	for _, diagnostic := range review.Diagnostics {
		require.NotContains(t, diagnostic.Text, missingAliasFor)
	}
}
