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
	t.Setenv("GOMODCACHE", cache)
	reviewed := filepath.Join(dir, "sdk", "exporter")
	write := func(name, content string) {
		t.Helper()
		require.NoError(t, os.MkdirAll(filepath.Dir(name), 0755))
		require.NoError(t, os.WriteFile(name, []byte(content), 0644))
	}
	sourceModule := "example.com/sdk/source"
	for version, underlying := range map[string]string{"v1.0.0": "[]byte", "v1.1.0": "[]rune"} {
		modDir := filepath.Join(cache, filepath.FromSlash(sourceModule)) + "@" + version
		write(filepath.Join(modDir, "go.mod"), "module "+sourceModule+"\n\ngo 1.21\n")
		write(filepath.Join(modDir, "types.go"), "package source\ntype Data "+underlying+"\n")
	}
	// A neighboring checkout must not override the version required by the reviewed module.
	neighbor := filepath.Join(dir, "sdk", "source")
	write(filepath.Join(neighbor, "go.mod"), "module "+sourceModule+"\n\ngo 1.21\n")
	write(filepath.Join(neighbor, "types.go"), "package source\ntype Data []bool\n")
	write(filepath.Join(reviewed, "alias.go"), "package exporter\nimport \""+sourceModule+"\"\ntype Public = source.Data\n")
	for _, test := range []struct{ version, declaration string }{
		{"v1.0.0", "typePublic=[]byte"},
		{"v1.1.0", "typePublic=[]rune"},
	} {
		write(filepath.Join(reviewed, "go.mod"), "module example.com/sdk/exporter\n\ngo 1.21\nrequire "+sourceModule+" "+test.version+"\n")
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
	t.Setenv("GOMODCACHE", cache)
	reviewed := filepath.Join(dir, "sdk", "exporter")
	write := func(name, content string) {
		t.Helper()
		require.NoError(t, os.MkdirAll(filepath.Dir(name), 0755))
		require.NoError(t, os.WriteFile(name, []byte(content), 0644))
	}
	sourceModule := "example.com/sdk/source"
	middleModule := "example.com/sdk/middle"
	sourceDir := filepath.Join(cache, filepath.FromSlash(sourceModule)) + "@v1.0.0"
	middleDir := filepath.Join(cache, filepath.FromSlash(middleModule)) + "@v1.0.0"
	write(filepath.Join(sourceDir, "go.mod"), "module "+sourceModule+"\n\ngo 1.21\nrequire "+middleModule+" v1.0.0\nrequire example.com/sdk/unrelated v1.0.0\n")
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
	write(filepath.Join(reviewed, "go.mod"), "module example.com/sdk/exporter\n\ngo 1.21\nrequire "+sourceModule+" v1.0.0\n")
	write(filepath.Join(reviewed, "alias.go"), "package exporter\nimport \""+sourceModule+"\"\ntype Public = source.Data\n")
	review, err := createReview(reviewed)
	require.NoError(t, err)
	require.Equal(t, "typePublic=[]rune", withoutAliasTarget(feedbackLineText(feedbackLine(t, review, "exporter.Public"))))
}

func TestReviewedInternalAliasChainToDependency(t *testing.T) {
	dir := t.TempDir()
	cache := filepath.Join(dir, "cache")
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
	write(filepath.Join(reviewed, "go.mod"), "module example.com/sdk/exporter\n\ngo 1.21\nrequire "+dependency+" v1.0.0\n")
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
