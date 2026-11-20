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
import elsewhere "example.com/elsewhere"
type External = elsewhere.Entry
`, nil)
	_, err := createReview(dir)
	require.ErrorContains(t, err, "couldn't resolve dependency for alias example.com/elsewhere.Entry")
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
		{"hidden composite alias component", "type hidden struct{}\ntype Public = []hidden\n", "hidden component type"},
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

func TestUnnamedImportUsesDeclaredPackageName(t *testing.T) {
	dir := t.TempDir()
	t.Setenv("GOPROXY", "off")
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/importer\n\ngo 1.21\nrequire example.com/go-amqp v1.0.0\nreplace example.com/go-amqp => ../go-amqp\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dir, "alias.go"), []byte(`
package importer
import "example.com/go-amqp"
type Public = amqp.Data
`), 0644))
	packageDir := filepath.Join(filepath.Dir(dir), "go-amqp")
	require.NoError(t, os.MkdirAll(packageDir, 0755))
	require.NoError(t, os.WriteFile(filepath.Join(packageDir, "go.mod"), []byte("module example.com/go-amqp\n\ngo 1.21\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(packageDir, "types.go"), []byte("package amqp\ntype Data []byte\n"), 0644))

	review, err := createReview(dir)
	require.NoError(t, err)
	require.Equal(t, "typePublic=[]byte", withoutAliasTarget(feedbackLineText(feedbackLine(t, review, "importer.Public"))))
}

func TestUnnamedImportNameIgnoresHostBuildConstraints(t *testing.T) {
	dir := t.TempDir()
	t.Setenv("GOOS", "linux")
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/importer\n\ngo 1.21\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dir, "alias.go"), []byte(`
package importer
import "syscall/js"
type Public = js.Value
`), 0644))

	review, err := createReview(dir)
	require.NoError(t, err)
	require.Equal(t, "typePublic=syscall/js.Value", withoutAliasTarget(feedbackLineText(feedbackLine(t, review, "importer.Public"))))
}

func TestPackageNamePrefersGoListForBuildExcludedPackages(t *testing.T) {
	dir := t.TempDir()
	t.Setenv("GOPROXY", "off")
	dependency := filepath.Join(dir, "dependency")
	importer := filepath.Join(dir, "importer")
	write := func(name, content string) {
		t.Helper()
		require.NoError(t, os.MkdirAll(filepath.Dir(name), 0755))
		require.NoError(t, os.WriteFile(name, []byte(content), 0644))
	}
	write(filepath.Join(importer, "go.mod"), "module example.com/importer\n\ngo 1.21\nrequire example.com/dependency v1.0.0\nreplace example.com/dependency => "+filepath.ToSlash(dependency)+"\n")
	write(filepath.Join(importer, "importer.go"), "package importer\n")
	write(filepath.Join(dependency, "go.mod"), "module example.com/dependency\n\ngo 1.21\n")
	write(filepath.Join(dependency, "dependency.go"), "package dependency\n")
	write(filepath.Join(dependency, "tools.go"), "//go:build tools\n\npackage tools\n")

	pkg, err := NewPkg(importer, "example.com/importer", importer)
	require.NoError(t, err)
	name, err := pkg.packageName("example.com/dependency")
	require.NoError(t, err)
	require.Equal(t, "dependency", name)
}

func TestUnusedDependencyImportsAreResolvedLazily(t *testing.T) {
	dir := t.TempDir()
	t.Setenv("GOPROXY", "off")
	reviewed := filepath.Join(dir, "exporter")
	dependency := filepath.Join(dir, "source")
	write := func(name, content string) {
		t.Helper()
		require.NoError(t, os.MkdirAll(filepath.Dir(name), 0755))
		require.NoError(t, os.WriteFile(name, []byte(content), 0644))
	}
	write(filepath.Join(dependency, "go.mod"), "module example.com/sdk/source\n\ngo 1.21\nrequire example.com/helper v1.0.0\n")
	write(filepath.Join(dependency, "types.go"), `
package source
import _ "example.com/helper"
type Data []byte
func (Data) helper() {}
`)
	write(filepath.Join(reviewed, "go.mod"), "module example.com/sdk/exporter\n\ngo 1.21\nrequire example.com/sdk/source v1.0.0\nreplace example.com/sdk/source => "+filepath.ToSlash(dependency)+"\n")
	write(filepath.Join(reviewed, "alias.go"), `
package exporter
import source "example.com/sdk/source"
type Public = source.Data
`)

	review, err := createReview(reviewed)
	require.NoError(t, err)
	require.Contains(t, feedbackLineText(feedbackLine(t, review, "exporter.Public")), "[]byte")
}

func TestInferredDeclarationValuesDoNotResolveImports(t *testing.T) {
	dir := t.TempDir()
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/contract\n\ngo 1.21\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dir, "types.go"), []byte(`
package contract
var defaults struct { Value int }
var private = defaults.Value
var handler = func(w interface { WriteHeader(int) }) { w.WriteHeader(200) }
`), 0644))

	_, err := createReview(dir)
	require.NoError(t, err)
}

func TestUnnamedImportsInTypeParameterConstraintsAreResolved(t *testing.T) {
	dir := t.TempDir()
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/generics\n\ngo 1.21\n"), 0644))
	require.NoError(t, os.MkdirAll(filepath.Join(dir, "constraints"), 0755))
	require.NoError(t, os.WriteFile(filepath.Join(dir, "types.go"), []byte(`
package generics
import "example.com/generics/constraints"
type Box[T constraints.Constraint] struct{}
type Container[T constraints.Constraint] interface{}
`), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dir, "constraints", "constraints.go"), []byte(`
package constraints
type Constraint interface { ~int }
`), 0644))

	review, err := createReview(dir)
	require.NoError(t, err)
	box := feedbackLine(t, review, "generics.Box")
	require.Contains(t, feedbackLineText(box), "<generics/constraints.Constraint>")
	container := feedbackLine(t, review, "generics.Container")
	require.True(t, searchTokens([]ReviewLine{container}, func(token ReviewToken) bool {
		return token.NavigateToID == "generics/constraints.Constraint"
	}))
}

func TestUnusedInternalAliasesToUnsupportedDependenciesAreSkipped(t *testing.T) {
	dir := t.TempDir()
	dependency := filepath.Join(filepath.Dir(dir), "dependency")
	write := func(name, content string) {
		t.Helper()
		require.NoError(t, os.MkdirAll(filepath.Dir(name), 0755))
		require.NoError(t, os.WriteFile(name, []byte(content), 0644))
	}
	write(filepath.Join(dependency, "go.mod"), "module example.com/dependency\n\ngo 1.21\n")
	write(filepath.Join(dependency, "types.go"), "package dependency\ntype Pointer *int\n")
	write(filepath.Join(dir, "go.mod"), "module example.com/aliastest\n\ngo 1.21\nrequire example.com/dependency v1.0.0\nreplace example.com/dependency => "+filepath.ToSlash(dependency)+"\n")
	write(filepath.Join(dir, "types.go"), "package aliastest\ntype Public int\n")
	write(filepath.Join(dir, "internal", "types", "types.go"), `
package types
import dependency "example.com/dependency"
type unused = dependency.Pointer
`)

	review, err := createReview(dir)
	require.NoError(t, err)
	require.Contains(t, feedbackLineText(feedbackLine(t, review, "aliastest.Public")), "int")
}

func TestDependencyPackageNameUsesReviewedModuleResolution(t *testing.T) {
	dir := t.TempDir()
	reviewed := filepath.Join(dir, "exporter")
	dependency := filepath.Join(dir, "source")
	other := filepath.Join(dir, "go-amqp")
	write := func(name, content string) {
		t.Helper()
		require.NoError(t, os.MkdirAll(filepath.Dir(name), 0755))
		require.NoError(t, os.WriteFile(name, []byte(content), 0644))
	}
	t.Setenv("GOPROXY", "off")
	write(filepath.Join(other, "go.mod"), "module example.com/go-amqp\n\ngo 1.21\n")
	write(filepath.Join(other, "types.go"), "package amqp\ntype Value struct{}\n")
	write(filepath.Join(dependency, "go.mod"), "module example.com/sdk/source\n\ngo 1.21\nrequire example.com/go-amqp v1.0.0\n")
	write(filepath.Join(dependency, "types.go"), `
package source
import "example.com/go-amqp"
type Data []byte
func (Data) Use(amqp.Value) {}
`)
	write(filepath.Join(reviewed, "go.mod"), "module example.com/sdk/exporter\n\ngo 1.21\nrequire (\n\texample.com/sdk/source v1.0.0\n\texample.com/go-amqp v1.0.0\n)\nreplace example.com/sdk/source => "+filepath.ToSlash(dependency)+"\nreplace example.com/go-amqp => "+filepath.ToSlash(other)+"\n")
	write(filepath.Join(reviewed, "alias.go"), "package exporter\nimport \"example.com/sdk/source\"\ntype Public = source.Data\n")

	review, err := createReview(reviewed)
	require.NoError(t, err)
	method := feedbackMethod(t, feedbackLine(t, review, "exporter.Public"), "Public", "Use")
	require.Contains(t, feedbackLineText(method), "example.com/go-amqp.Value")
}

func TestHoistedSignatureRewriteMatchesCompleteSelector(t *testing.T) {
	target := &Pkg{
		modulePath: "example.com/target",
		relName:    "target",
		TypeAliases: []*TypeAlias{{
			Name:          "Value",
			QualifiedName: "example.com/other.Value",
			isAlias:       true,
		}},
	}
	target.TypeAliases[0].Package = target
	target.modulePackages = map[string]*Pkg{"example.com/target": target}
	source := &Pkg{modulePath: "example.com/source", relName: "source"}
	fn := Func{imports: map[string]string{"other": "example.com/other"}}
	typ := "struct { A other.Value; B other.ValueExtra }"

	got := fn.translateAliasSignatureType(source, target, typ, typ, "Public", nil)
	require.Contains(t, got, "A <target.Value>Value")
	require.Contains(t, got, "B example.com/other.ValueExtra")
	require.NotContains(t, got, "<target.Value>ValueExtra")
}

func TestVersionedModuleTypeIdentityFindsExposedAliases(t *testing.T) {
	dependency := &Pkg{
		modulePath: "example.com/model/v2",
		relName:    "model/types",
	}
	target := &Pkg{
		modulePath: "example.com/api",
		relName:    "api",
		TypeAliases: []*TypeAlias{{
			Name:          "Item",
			QualifiedName: "example.com/model/v2/types.Item",
			isAlias:       true,
		}},
	}
	target.TypeAliases[0].Package = target
	target.modulePackages = map[string]*Pkg{
		"example.com/api":         target,
		"example.com/model/types": dependency,
	}
	dependency.modulePackages = target.modulePackages
	qualifiedName := "example.com/model/v2/types.Item"

	require.True(t, targetExportsQualifiedType(target, qualifiedName))
	endpoint, name, found := findExposedSignatureType(target, qualifiedName)
	require.True(t, found)
	require.Same(t, target, endpoint)
	require.Equal(t, "Item", name)
}

func TestDefinedGenericInstantiationRemainsSupported(t *testing.T) {
	dir := t.TempDir()
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/generics\n\ngo 1.21\n"), 0644))
	for name, content := range map[string]string{
		"alias.go": `package generics
import (
	"example.com/generics/base"
	"example.com/generics/generated"
)
type Client base.Client[generated.BlobClient]
`,
		filepath.Join("base", "base.go"):           "package base\ntype Client[T any] struct{}\n",
		filepath.Join("generated", "generated.go"): "package generated\ntype BlobClient struct{}\n",
	} {
		file := filepath.Join(dir, name)
		require.NoError(t, os.MkdirAll(filepath.Dir(file), 0755))
		require.NoError(t, os.WriteFile(file, []byte(content), 0644))
	}

	review, err := createReview(dir)
	require.NoError(t, err)
	line := feedbackLine(t, review, "generics.Client")
	require.Contains(t, feedbackLineText(line), "Client")
	require.Contains(t, feedbackLineText(line), "BlobClient")
}

func TestVersionedDependencyAliasThroughInternalDefinedSelector(t *testing.T) {
	dir := t.TempDir()
	t.Setenv("GOPROXY", "off")
	reviewed := filepath.Join(dir, "exporter")
	source := filepath.Join(dir, "source")
	model := filepath.Join(dir, "model")
	write := func(name, content string) {
		t.Helper()
		require.NoError(t, os.MkdirAll(filepath.Dir(name), 0755))
		require.NoError(t, os.WriteFile(name, []byte(content), 0644))
	}
	write(filepath.Join(model, "go.mod"), "module example.com/model/v2\n\ngo 1.21\n")
	write(filepath.Join(model, "types", "types.go"), "package types\ntype Data []byte\n")
	write(filepath.Join(source, "go.mod"), "module example.com/sdk/source/v2\n\ngo 1.21\nrequire example.com/model/v2 v2.0.0\n")
	write(filepath.Join(source, "source.go"), `
package source
import bridge "example.com/sdk/source/v2/internal/bridge"
type Public = bridge.Defined
`)
	write(filepath.Join(source, "internal", "bridge", "bridge.go"), `
package bridge
import model "example.com/model/v2/types"
type Defined model.Data
func (Defined) Method() {}
`)
	write(filepath.Join(reviewed, "go.mod"), "module example.com/sdk/exporter\n\ngo 1.21\nrequire (\n\texample.com/sdk/source/v2 v2.0.0\n\texample.com/model/v2 v2.0.0\n)\nreplace example.com/sdk/source/v2 => "+filepath.ToSlash(source)+"\nreplace example.com/model/v2 => "+filepath.ToSlash(model)+"\n")
	write(filepath.Join(reviewed, "alias.go"), `
package exporter
import source "example.com/sdk/source/v2"
type Public = source.Public
`)

	review, err := createReview(reviewed)
	require.NoError(t, err)
	line := feedbackLine(t, review, "exporter.Public")
	require.Contains(t, withoutAliasTarget(feedbackLineText(line)), "[]byte")
	feedbackMethod(t, line, "Public", "Method")
}

func TestVersionedModuleResolvesInternalDefinedSelector(t *testing.T) {
	dir := t.TempDir()
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/sdk/contract/v2\n\ngo 1.21\n"), 0644))
	for name, content := range map[string]string{
		"alias.go": `package contract
import types "example.com/sdk/contract/v2/internal/types"
type Public = types.Defined
`,
		filepath.Join("internal", "types", "types.go"): `package types
import base "example.com/sdk/contract/v2/internal/base"
type Defined base.Data
`,
		filepath.Join("internal", "base", "base.go"): "package base\ntype Data []byte\n",
	} {
		file := filepath.Join(dir, name)
		require.NoError(t, os.MkdirAll(filepath.Dir(file), 0755))
		require.NoError(t, os.WriteFile(file, []byte(content), 0644))
	}

	review, err := createReview(dir)
	require.NoError(t, err)
	require.Contains(t, withoutAliasTarget(feedbackLineText(feedbackLine(t, review, "contract.Public"))), "[]byte")
}

func TestHoistedMethodTypesUseExposedAliasesAndImportPaths(t *testing.T) {
	dir := t.TempDir()
	t.Setenv("GOPROXY", "off")
	reviewed := filepath.Join(dir, "exporter")
	dependency := filepath.Join(dir, "source")
	write := func(name, content string) {
		t.Helper()
		require.NoError(t, os.MkdirAll(filepath.Dir(name), 0755))
		require.NoError(t, os.WriteFile(name, []byte(content), 0644))
	}
	write(filepath.Join(dependency, "go.mod"), "module example.com/sdk/source\n\ngo 1.21\n")
	sourceFile := filepath.Join(dependency, "types", "types.go")
	write(sourceFile, `
package types
import other "example.com/old"
type Data []byte
type Record struct{}
func (Data) Get() Record { return Record{} }
func (Data) Use(values ...other.Value) other.Value { return other.Value{} }
`)
	write(filepath.Join(reviewed, "go.mod"), "module example.com/sdk/exporter\n\ngo 1.21\nrequire example.com/sdk/source v1.0.0\nreplace example.com/sdk/source => "+filepath.ToSlash(dependency)+"\n")
	write(filepath.Join(reviewed, "alias.go"), `
package exporter
import "example.com/sdk/source/types"
type Public = types.Data
type Entry = types.Record
`)

	review, err := createReview(reviewed)
	require.NoError(t, err)
	line := feedbackLine(t, review, "exporter.Public")
	get := feedbackLineText(feedbackMethod(t, line, "Public", "Get"))
	require.Contains(t, get, "Entry")
	require.NotContains(t, get, "Record")
	use := feedbackLineText(feedbackMethod(t, line, "Public", "Use"))
	require.Contains(t, use, "example.com/old.Value")
	require.NotContains(t, use, "other.Value")

	source, err := os.ReadFile(sourceFile)
	require.NoError(t, err)
	write(sourceFile, strings.ReplaceAll(string(source), "example.com/old", "example.com/new"))
	changed, err := createReview(reviewed)
	require.NoError(t, err)
	changedUse := feedbackLineText(feedbackMethod(t, feedbackLine(t, changed, "exporter.Public"), "Public", "Use"))
	require.Contains(t, changedUse, "example.com/new.Value")
	require.NotContains(t, changedUse, "example.com/old.Value")
}

func TestUnnamedImportNameLookupFailureIsReported(t *testing.T) {
	dir := t.TempDir()
	t.Setenv("GOPROXY", "off")
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/importer\n\ngo 1.21\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dir, "alias.go"), []byte(`
package importer
import "example.com/go-amqp"
type Public = amqp.Data
`), 0644))

	_, err := createReview(dir)
	require.ErrorContains(t, err, "resolving unnamed import example.com/go-amqp")
}

func TestGenericComponentsAndSignaturesAreValidatedRecursively(t *testing.T) {
	dir := t.TempDir()
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/generics\n\ngo 1.21\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dir, "types.go"), []byte(`
package generics
import "sync/atomic"
type Box[T any] struct { Value T }
type Public []Box[int]
type GenericClient[T any] struct{}
type BaseClient struct{}
type Client GenericClient[BaseClient]
func (Client) Pointer() *atomic.Pointer[int] { return nil }
`), 0644))

	review, err := createReview(dir)
	require.NoError(t, err)
	require.Contains(t, feedbackLineText(feedbackLine(t, review, "generics.Public")), "Box[int]")
	require.Contains(t, feedbackLineText(feedbackMethod(t, feedbackLine(t, review, "generics.Client"), "Client", "Pointer")), "atomic.Pointer[int]")
}

func TestCompositeUnqualifiedInternalTypeUsesPublicAlias(t *testing.T) {
	dir := t.TempDir()
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/aliastest\n\ngo 1.21\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dir, "alias.go"), []byte(`
package aliastest
import "example.com/aliastest/model"
type Public = model.Data
`), 0644))
	modelDir := filepath.Join(dir, "model")
	require.NoError(t, os.MkdirAll(modelDir, 0755))
	require.NoError(t, os.WriteFile(filepath.Join(modelDir, "model.go"), []byte(`
package model
type hidden struct{}
type Data []hidden
type Visible = hidden
`), 0644))
	review, err := createReview(dir)
	require.NoError(t, err)
	line := feedbackLine(t, review, "aliastest.Public")
	require.Contains(t, feedbackLineText(line), "aliastest/model.Visible")
	require.True(t, searchTokens([]ReviewLine{line}, func(token ReviewToken) bool {
		return token.NavigateToID == "aliastest/model.Visible"
	}))
}

func TestDefinedTypeIsNotExposedByAliasToItsUnderlyingType(t *testing.T) {
	dir := aliasFeedbackFixture(t, "import \"example.com/aliastest/model\"\ntype Visible = model.Hidden\ntype Public = types.Data\n", `
import "example.com/aliastest/model"
type Defined model.Hidden
type Data []Defined
`, map[string]string{
		filepath.Join("model", "model.go"): "package model\ntype Hidden int\n",
	})
	_, err := createReview(dir)
	require.ErrorContains(t, err, "hidden component type aliastest/internal/types.Defined")
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

func TestTypeTokensRecognizeGoWhitespaceAndSemicolons(t *testing.T) {
	tokens := parseAndMakeTypeTokens("Box\tmap[string]int\nOther;Next")
	require.True(t, searchTokens([]ReviewLine{{Tokens: tokens}}, func(token ReviewToken) bool {
		return token.Value == "Box" && token.HasSuffixSpace
	}))
	require.True(t, searchTokens([]ReviewLine{{Tokens: tokens}}, func(token ReviewToken) bool {
		return token.Kind == TokenKindPunctuation && token.Value == ";"
	}))
	require.True(t, searchTokens([]ReviewLine{{Tokens: tokens}}, func(token ReviewToken) bool {
		return token.Value == "Next"
	}))
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
