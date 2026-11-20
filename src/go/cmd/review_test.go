// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"os"
	"path/filepath"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestExternalAliasDependencyVersion(t *testing.T) {
	dir := t.TempDir()
	cache := filepath.Join(dir, "cache")
	t.Setenv("GOPROXY", "off")
	t.Setenv("GOMODCACHE", cache)
	reviewed := filepath.Join(dir, "sdk", "exporter")
	write := func(name, content string) {
		t.Helper()
		require.NoError(t, os.MkdirAll(filepath.Dir(name), 0755))
		require.NoError(t, os.WriteFile(name, []byte(content), 0644))
	}
	sourceModule := "example.com/sdk/source"
	versionDirs := map[string]string{}
	for version, underlying := range map[string]string{"v1.0.0": "[]byte", "v1.1.0": "[]rune"} {
		modDir := filepath.Join(cache, filepath.FromSlash(sourceModule)) + "@" + version
		versionDirs[version] = modDir
		write(filepath.Join(modDir, "go.mod"), "module "+sourceModule+"\n\ngo 1.21\n")
		write(filepath.Join(modDir, "types.go"), "package source\ntype Data "+underlying+"\n")
	}
	// A neighboring checkout must not override the version required by the reviewed module.
	neighbor := filepath.Join(dir, "sdk", "source")
	write(filepath.Join(neighbor, "go.mod"), "module "+sourceModule+"\n\ngo 1.21\n")
	write(filepath.Join(neighbor, "types.go"), "package source\ntype Data []bool\n")
	workspace := filepath.Join(dir, "go.work")
	write(workspace, "go 1.21\nuse ./sdk/source\n")
	t.Setenv("GOWORK", workspace)
	write(filepath.Join(reviewed, "alias.go"), "package exporter\nimport \""+sourceModule+"\"\ntype Public = source.Data\n")
	for _, test := range []struct{ version, declaration string }{
		{"v1.0.0", "typePublic=[]byte"},
		{"v1.1.0", "typePublic=[]rune"},
	} {
		write(filepath.Join(reviewed, "go.mod"), "module example.com/sdk/exporter\n\ngo 1.21\nrequire "+sourceModule+" "+test.version+"\nreplace "+sourceModule+" => "+filepath.ToSlash(versionDirs[test.version])+"\n")
		review, err := createReview(reviewed)
		require.NoError(t, err)
		require.Equal(t, test.declaration, withoutAliasTarget(feedbackLineText(feedbackLine(t, review, "exporter.Public"))))
	}
	t.Run("explicit local replacement", func(t *testing.T) {
		write(filepath.Join(reviewed, "go.mod"), "module example.com/sdk/exporter\n\ngo 1.21\nrequire "+sourceModule+" v1.0.0\nreplace "+sourceModule+" => ../source\n")
		review, err := createReview(reviewed)
		require.NoError(t, err)
		require.Equal(t, "typePublic=[]bool", withoutAliasTarget(feedbackLineText(feedbackLine(t, review, "exporter.Public"))))
	})
	t.Run("version-specific replacement takes precedence and uses replacement module path", func(t *testing.T) {
		replacement := filepath.Join(dir, "sdk", "forked-source")
		write(filepath.Join(replacement, "go.mod"), "module example.com/fork/source\n\ngo 1.21\n")
		write(filepath.Join(replacement, "types.go"), "package source\ntype Data []complex64\n")
		write(filepath.Join(reviewed, "go.mod"), "module example.com/sdk/exporter\n\ngo 1.21\nrequire "+sourceModule+" v1.0.0\nreplace "+sourceModule+" => ../source\nreplace "+sourceModule+" v1.0.0 => ../forked-source\n")
		review, err := createReview(reviewed)
		require.NoError(t, err)
		require.Equal(t, "typePublic=[]complex64", withoutAliasTarget(feedbackLineText(feedbackLine(t, review, "exporter.Public"))))
	})
}

func TestExternalAliasChainSkipsUnreachableDependencyAliases(t *testing.T) {
	dir := t.TempDir()
	cache := filepath.Join(dir, "cache")
	t.Setenv("GOPROXY", "off")
	t.Setenv("GOMODCACHE", cache)
	reviewed := filepath.Join(dir, "sdk", "exporter")
	write := func(name, content string) {
		t.Helper()
		require.NoError(t, os.MkdirAll(filepath.Dir(name), 0755))
		require.NoError(t, os.WriteFile(name, []byte(content), 0644))
	}
	sourceModule := "example.com/sdk/source"
	middleModule := "example.com/sdk/middle"
	bridgeModule := "example.com/sdk/bridge"
	sourceDir := filepath.Join(cache, filepath.FromSlash(sourceModule)) + "@v1.0.0"
	middleDir := filepath.Join(cache, filepath.FromSlash(middleModule)) + "@v1.0.0"
	selectedMiddleDir := filepath.Join(cache, filepath.FromSlash(middleModule)) + "@v1.1.0"
	bridgeDir := filepath.Join(dir, "bridge")
	unrelatedModule := "example.com/sdk/unrelated"
	unrelatedDir := filepath.Join(dir, "unrelated")
	write(filepath.Join(bridgeDir, "go.mod"), "module "+bridgeModule+"\n\ngo 1.21\nrequire "+middleModule+" v1.1.0\n")
	write(filepath.Join(unrelatedDir, "go.mod"), "module "+unrelatedModule+"\n\ngo 1.21\n")
	write(filepath.Join(unrelatedDir, "types.go"), "package unrelated\ntype Missing int\n")
	write(filepath.Join(sourceDir, "go.mod"), "module "+sourceModule+"\n\ngo 1.21\nrequire "+middleModule+" v1.0.0\nrequire "+unrelatedModule+" v1.0.0\nreplace "+middleModule+" => "+filepath.ToSlash(middleDir)+"\nreplace "+unrelatedModule+" => "+filepath.ToSlash(unrelatedDir)+"\n")
	write(filepath.Join(sourceDir, "types.go"), `package source
import (
	"example.com/sdk/source/internal/model"
	"example.com/sdk/unrelated"
)
type Data = model.Result
type Unused = unrelated.Missing
`)
	write(filepath.Join(sourceDir, "internal", "model", "types.go"), `package model
import "example.com/sdk/middle"
type Result = middle.Result
`)
	write(filepath.Join(middleDir, "go.mod"), "module "+middleModule+"\n\ngo 1.21\n")
	write(filepath.Join(middleDir, "types.go"), "package middle\ntype Result []rune\n")
	write(filepath.Join(selectedMiddleDir, "go.mod"), "module "+middleModule+"\n\ngo 1.21\n")
	write(filepath.Join(selectedMiddleDir, "types.go"), "package middle\ntype Result []complex64\n")
	write(filepath.Join(reviewed, "go.mod"), "module example.com/sdk/exporter\n\ngo 1.21\nrequire (\n\t"+sourceModule+" v1.0.0\n\t"+bridgeModule+" v1.0.0\n\t"+middleModule+" v1.1.0\n\t"+unrelatedModule+" v1.0.0\n)\nreplace "+sourceModule+" => "+filepath.ToSlash(sourceDir)+"\nreplace "+bridgeModule+" => "+filepath.ToSlash(bridgeDir)+"\nreplace "+middleModule+" => "+filepath.ToSlash(selectedMiddleDir)+"\nreplace "+unrelatedModule+" => "+filepath.ToSlash(unrelatedDir)+"\n")
	write(filepath.Join(reviewed, "alias.go"), "package exporter\nimport \""+sourceModule+"\"\ntype Public = source.Data\n")
	review, err := createReview(reviewed)
	require.NoError(t, err)
	require.Equal(t, "typePublic=[]complex64", withoutAliasTarget(feedbackLineText(feedbackLine(t, review, "exporter.Public"))))
}

func TestReviewedInternalAliasChainToDependency(t *testing.T) {
	dir := t.TempDir()
	cache := filepath.Join(dir, "cache")
	t.Setenv("GOPROXY", "off")
	t.Setenv("GOMODCACHE", cache)
	reviewed := filepath.Join(dir, "sdk", "exporter")
	write := func(name, content string) {
		t.Helper()
		require.NoError(t, os.MkdirAll(filepath.Dir(name), 0755))
		require.NoError(t, os.WriteFile(name, []byte(content), 0644))
	}
	dependency := "example.com/sdk/blob"
	dependencyDir := filepath.Join(cache, filepath.FromSlash(dependency)) + "@v1.0.0"
	write(filepath.Join(dependencyDir, "go.mod"), "module "+dependency+"\n\ngo 1.21\n")
	write(filepath.Join(dependencyDir, "types.go"), "package blob\ntype Data []byte\n")
	write(filepath.Join(reviewed, "go.mod"), "module example.com/sdk/exporter\n\ngo 1.21\nrequire "+dependency+" v1.0.0\nreplace "+dependency+" => "+filepath.ToSlash(dependencyDir)+"\n")
	write(filepath.Join(reviewed, "directory", "types.go"), `
package directory
import internal "example.com/sdk/exporter/internal/path"
type Public internal.Type
`)
	write(filepath.Join(reviewed, "internal", "path", "types.go"), `
package path
import "example.com/sdk/blob"
type Type = blob.Data
`)
	review, err := createReview(reviewed)
	require.NoError(t, err)
	require.Equal(t, "typePublic[]byte", withoutAliasTarget(feedbackLineText(feedbackLine(t, review, "exporter/directory.Public"))))
}

func TestDefinedTypeAliasPreservesTypeIdentityAndMethods(t *testing.T) {
	dir := t.TempDir()
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/duration\n\ngo 1.21\n"), 0644))
	require.NoError(t, os.MkdirAll(filepath.Join(dir, "internal", "types"), 0755))
	require.NoError(t, os.WriteFile(filepath.Join(dir, "alias.go"), []byte(`
package duration
import "example.com/duration/internal/types"
type Public = types.MyDuration
`), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dir, "internal", "types", "types.go"), []byte(`
package types
import "time"
type MyDuration time.Duration
func (MyDuration) Add(time.Duration) MyDuration { return 0 }
`), 0644))

	review, err := createReview(dir)
	require.NoError(t, err)
	line := feedbackLine(t, review, "duration.Public")
	require.Equal(t, "typePublic=time.Duration", withoutAliasTarget(feedbackLineText(line)))
	require.Contains(t, feedbackLineText(line), "alias of example.com/duration/internal/types.MyDuration")
	require.Contains(t, feedbackLineText(feedbackMethod(t, line, "Public", "Add")), "time.Duration")
}

func TestPrivateDefinedSelectorAliasPreservesUnderlyingAndMethods(t *testing.T) {
	dir := t.TempDir()
	require.NoError(t, os.WriteFile(filepath.Join(dir, "go.mod"), []byte("module example.com/duration\n\ngo 1.21\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dir, "types.go"), []byte(`
package duration
import "time"
type hidden time.Duration
func (hidden) Add(time.Duration) hidden { return 0 }
type Public = hidden
`), 0644))
	review, err := createReview(dir)
	require.NoError(t, err)
	line := feedbackLine(t, review, "duration.Public")
	require.Equal(t, "typePublic=time.Duration", withoutAliasTarget(feedbackLineText(line)))
	require.Contains(t, feedbackLineText(feedbackMethod(t, line, "Public", "Add")), "time.Duration")
}

func TestExternalAliasChainCanReturnToModuleRoot(t *testing.T) {
	dir := t.TempDir()
	t.Setenv("GOPROXY", "off")
	reviewed := filepath.Join(dir, "exporter")
	dependency := filepath.Join(dir, "source")
	require.NoError(t, os.MkdirAll(filepath.Join(dependency, "sub"), 0755))
	require.NoError(t, os.WriteFile(filepath.Join(dependency, "go.mod"), []byte("module example.com/sdk/source\n\ngo 1.21\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dependency, "root.go"), []byte(`
package source
import "time"
type Duration = time.Duration
`), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dependency, "sub", "sub.go"), []byte(`
package sub
import "example.com/sdk/source"
type Duration = source.Duration
`), 0644))
	require.NoError(t, os.MkdirAll(reviewed, 0755))
	require.NoError(t, os.WriteFile(filepath.Join(reviewed, "go.mod"), []byte("module example.com/sdk/exporter\n\ngo 1.21\nrequire example.com/sdk/source v1.0.0\nreplace example.com/sdk/source => "+filepath.ToSlash(dependency)+"\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(reviewed, "alias.go"), []byte(`
package exporter
import "example.com/sdk/source/sub"
type Public = sub.Duration
`), 0644))

	review, err := createReview(reviewed)
	require.NoError(t, err)
	require.Equal(t, "typePublic=time.Duration", withoutAliasTarget(feedbackLineText(feedbackLine(t, review, "exporter.Public"))))
}

func TestExternalDefinedTypeAliasPreservesTypeAndMethods(t *testing.T) {
	dir := t.TempDir()
	t.Setenv("GOPROXY", "off")
	reviewed := filepath.Join(dir, "exporter")
	dependency := filepath.Join(dir, "source")
	require.NoError(t, os.MkdirAll(dependency, 0755))
	require.NoError(t, os.WriteFile(filepath.Join(dependency, "go.mod"), []byte("module example.com/sdk/source\n\ngo 1.21\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dependency, "types.go"), []byte(`
package source
import "time"
type MyDuration time.Duration
func (MyDuration) Add(time.Duration) MyDuration { return 0 }
`), 0644))
	require.NoError(t, os.MkdirAll(reviewed, 0755))
	require.NoError(t, os.WriteFile(filepath.Join(reviewed, "go.mod"), []byte("module example.com/sdk/exporter\n\ngo 1.21\nrequire example.com/sdk/source v1.0.0\nreplace example.com/sdk/source => "+filepath.ToSlash(dependency)+"\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(reviewed, "alias.go"), []byte(`
package exporter
import "example.com/sdk/source"
type Public = source.MyDuration
`), 0644))

	review, err := createReview(reviewed)
	require.NoError(t, err)
	line := feedbackLine(t, review, "exporter.Public")
	require.Equal(t, "typePublic=time.Duration", withoutAliasTarget(feedbackLineText(line)))
	require.Contains(t, feedbackLineText(line), "alias of example.com/sdk/source.MyDuration")
	require.Contains(t, feedbackLineText(feedbackMethod(t, line, "Public", "Add")), "time.Duration")
}

func TestExternalDefinedSelectorAliasTracksUnderlyingType(t *testing.T) {
	dir := t.TempDir()
	t.Setenv("GOPROXY", "off")
	reviewed := filepath.Join(dir, "exporter")
	dependency := filepath.Join(dir, "source")
	require.NoError(t, os.MkdirAll(filepath.Join(dependency, "bridge"), 0755))
	require.NoError(t, os.MkdirAll(filepath.Join(dependency, "types"), 0755))
	require.NoError(t, os.WriteFile(filepath.Join(dependency, "go.mod"), []byte("module example.com/sdk/source\n\ngo 1.21\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(dependency, "bridge", "bridge.go"), []byte(`
package bridge
import "example.com/sdk/source/types"
type Defined types.Data
func (Defined) Method() {}
`), 0644))
	dataFile := filepath.Join(dependency, "types", "types.go")
	require.NoError(t, os.WriteFile(dataFile, []byte("package types\ntype Data []byte\n"), 0644))
	require.NoError(t, os.MkdirAll(reviewed, 0755))
	require.NoError(t, os.WriteFile(filepath.Join(reviewed, "go.mod"), []byte("module example.com/sdk/exporter\n\ngo 1.21\nrequire example.com/sdk/source v1.0.0\nreplace example.com/sdk/source => "+filepath.ToSlash(dependency)+"\n"), 0644))
	require.NoError(t, os.WriteFile(filepath.Join(reviewed, "alias.go"), []byte(`
package exporter
import "example.com/sdk/source/bridge"
type Public = bridge.Defined
`), 0644))

	for _, underlying := range []string{"[]byte", "[]rune"} {
		require.NoError(t, os.WriteFile(dataFile, []byte("package types\ntype Data "+underlying+"\n"), 0644))
		review, err := createReview(reviewed)
		require.NoError(t, err)
		line := feedbackLine(t, review, "exporter.Public")
		require.Equal(t, "typePublic="+underlying, withoutAliasTarget(feedbackLineText(line)))
		feedbackMethod(t, line, "Public", "Method")
	}
}
