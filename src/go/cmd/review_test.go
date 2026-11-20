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
		require.Equal(t, test.declaration, feedbackLineText(feedbackLine(t, review, "exporter.Public")))
	}
	t.Run("explicit local replacement", func(t *testing.T) {
		write(filepath.Join(reviewed, "go.mod"), "module example.com/sdk/exporter\n\ngo 1.21\nrequire "+sourceModule+" v1.0.0\nreplace "+sourceModule+" => ../source\n")
		review, err := createReview(reviewed)
		require.NoError(t, err)
		require.Equal(t, "typePublic=[]bool", feedbackLineText(feedbackLine(t, review, "exporter.Public")))
	})
}
