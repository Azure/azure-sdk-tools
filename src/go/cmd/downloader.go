// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Package download provides functions to acquire code from a Go module proxy.
package cmd

import (
	"context"
	"errors"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"os"
	"path"
	"path/filepath"
	"time"

	"golang.org/x/mod/module"
	"golang.org/x/mod/zip"
)

// cache looks like:
//     /tmp/apiviewgo
//     ├── azcore@v1.0.0
//     │   └── github.com/!azure/azure-sdk-for-go/sdk/azcore@v1.0.0
//     │       └── go.mod
//     └── zips
//         └── github.com/!azure/azure-sdk-for-go/sdk/azcore
//             └── v1.0.0.zip
//
// This is perhaps odd but zip.Unzip() requires the target directory be entirely
// empty, so e.g. keeping source and zips in a single directory is impossible.
// Note also that module paths are escaped by module.EscapePath (hence "A" becomes
// "!a"). Also, although we could in principle unzip modules to the local Go module
// cache, we don't do so because we don't want to affect other Go programs or to
// reimplement whatever `go mod download` behavior is necessary for correctness.

var errCachedModuleNotFound = errors.New("cached module not found")

// mustEscape escapes a module path. It panics when that fails.
func mustEscape(modPath string) string {
	escaped, err := module.EscapePath(modPath)
	if err != nil {
		panic(fmt.Errorf("failed to escape module path: %w", err))
	}
	return escaped
}

func cacheDir() string {
	return filepath.Join(os.TempDir(), "apiviewgo")
}

// cacheDirForSource returns the path to mod's directory in the local cache
func cacheDirForSource(mod module.Version) string {
	return filepath.Join(cacheDir(), mustEscape(mod.Path)) + "@" + mod.Version
}

// cachePathForZip returns the path to mod's zip file in the local cache
func cachePathForZip(mod module.Version) string {
	return filepath.Join(cacheDir(), "zips", mustEscape(mod.Path), mod.Version+".zip")
}

// modFromZip unzips the module at mod's zip file in the local cache
func modFromZip(mod module.Version) (*Module, error) {
	d := cacheDirForSource(mod)
	err := zip.Unzip(d, mod, cachePathForZip(mod))
	if err != nil {
		return nil, fmt.Errorf("failed to unzip module: %w", err)
	}
	if _, err := os.Stat(filepath.Join(d, "go.mod")); err != nil {
		return nil, fmt.Errorf("go.mod not found in %s", d)
	}
	return NewModule(d)
}

func cachedModule(mod module.Version) (*Module, error) {
	d := cacheDirForSource(mod)
	if _, err := os.Stat(filepath.Join(d, "go.mod")); err == nil {
		return NewModule(d)
	}
	zp := cachePathForZip(mod)
	if _, err := os.Stat(zp); err == nil {
		return modFromZip(mod)
	}
	return nil, nil
}

// TODO: test that this is called only when strictly required
func Download(mod module.Version) (*Module, error) {
	m, err := cachedModule(mod)
	// if the module isn't cached, download it; if the module is cached
	// but unparseable, return the error
	if err != nil && !errors.Is(err, errCachedModuleNotFound) {
		return nil, fmt.Errorf("failed to check cache: %w", err)
	}
	if m != nil {
		return m, nil
	}
	escaped, err := module.EscapePath(mod.Path)
	if err != nil {
		return nil, fmt.Errorf("unescapeable module path %q: %w", mod.Path, err)
	}
	u, err := url.Parse("https://" + path.Join("proxy.golang.org", escaped, "@v", mod.Version+".zip"))
	if err != nil {
		return nil, fmt.Errorf("failed to parse module URL: %w", err)
	}
	ctx, cancel := context.WithTimeout(context.Background(), time.Minute)
	defer cancel()
	req, err := http.NewRequestWithContext(ctx, http.MethodGet, u.String(), nil)
	if err != nil {
		return nil, fmt.Errorf("failed to create request: %w", err)
	}
	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		return nil, fmt.Errorf("failed to download module zip from %s: %w", u, err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != 200 {
		return nil, fmt.Errorf("unexpected status code: %d", resp.StatusCode)
	}
	zp := cachePathForZip(mod)
	err = os.MkdirAll(filepath.Dir(zp), 0700)
	if err != nil {
		return nil, fmt.Errorf("failed to create %q: %w", zp, err)
	}
	f, err := os.Create(zp)
	if err != nil {
		return nil, fmt.Errorf("failed to create zip file: %w", err)
	}
	defer f.Close()

	_, err = io.Copy(f, resp.Body)
	if err != nil {
		return nil, fmt.Errorf("failed to write zip file: %w", err)
	}
	return modFromZip(mod)
}
