// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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

var errCachedModuleNotFound = errors.New("cached module not found")

// GetExternalModule returns a Module representing mod. When GOMODCACHE is set,
// it looks for mod's source in the mod cache. Otherwise, it downloads mod from
// the module proxy.
func GetExternalModule(mod module.Version) (*Module, error) {
	m, err := cachedModule(mod)
	if err != nil && !errors.Is(err, errCachedModuleNotFound) {
		return nil, fmt.Errorf("failed to parse cached module %s: %w", mod.Path, err)
	}
	if m == nil {
		ctx, cancel := context.WithTimeout(context.Background(), time.Minute)
		defer cancel()
		m, err = downloadModule(ctx, mod)
	}
	return m, err
}

// downloadModule downloads mod from the Go module proxy, storing it in a temporary directory.
// That directory looks like:
//
//	~/apiviewgo{random suffix}
//	├── azcore@v1.0.0
//	│   └── github.com/!azure/azure-sdk-for-go/sdk/azcore@v1.0.0
//	│       └── go.mod
//	└── zip
//	    └── github.com/!azure/azure-sdk-for-go/sdk/azcore
//	        └── v1.0.0.zip
//
// This is perhaps odd but zip.Unzip() requires the target directory be entirely empty, so
// obvious tidier schemes are impossible. Although downloadModule could in principle unzip
// modules to the local Go module cache, it doesn't do so to avoid affecting other Go programs
// or reimplementing whatever `go mod download` behavior is necessary to ensure correctness.
func downloadModule(ctx context.Context, mod module.Version) (*Module, error) {
	d, err := downloadDir()
	if err != nil {
		return nil, err
	}
	// We don't keep downloaded content because an apiviewgo instance doesn't need to download
	// any mod twice (Review caches the Module this function returns) and we don't want to
	// maintain a cache given the low performance impact of downloading modules (very few SDK
	// modules export types defined elsewhere).
	defer func() {
		if err := os.RemoveAll(d); err != nil {
			fmt.Fprintf(os.Stderr, "failed to remove download directory %s: %v", d, err)
		}
	}()
	escaped, err := module.EscapePath(mod.Path)
	if err != nil {
		return nil, fmt.Errorf("unescapeable module path %q: %w", mod.Path, err)
	}
	u, err := url.Parse("https://" + path.Join("proxy.golang.org", escaped, "@v", mod.Version+".zip"))
	if err != nil {
		return nil, fmt.Errorf("failed to parse module URL: %w", err)
	}
	req, err := http.NewRequestWithContext(ctx, http.MethodGet, u.String(), nil)
	if err != nil {
		return nil, fmt.Errorf("failed to create request: %w", err)
	}
	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		return nil, fmt.Errorf("failed to download module zip from %s: %w", u, err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		msg := "module proxy responded %d"
		if resp.Body != nil {
			b, _ := io.ReadAll(resp.Body)
			msg += ": " + string(b)
		}
		return nil, fmt.Errorf(msg, resp.StatusCode)
	}
	zp := filepath.Join(d, "zip", mustEscape(mod.Path), mod.Version+".zip")
	err = os.MkdirAll(filepath.Dir(zp), 0700)
	if err != nil {
		return nil, fmt.Errorf("failed to create directory for %s: %w", zp, err)
	}
	f, err := os.Create(zp)
	if err != nil {
		return nil, fmt.Errorf("failed to create %s: %w", zp, err)
	}
	defer f.Close()
	_, err = io.Copy(f, resp.Body)
	if err != nil {
		return nil, fmt.Errorf("failed to write %s: %w", zp, err)
	}
	modver := path.Base(mod.Path) + "@" + mod.Version
	p := filepath.Join(d, modver, mustEscape(mod.Path)) + "@" + mod.Version
	err = zip.Unzip(p, mod, zp)
	if err != nil {
		return nil, fmt.Errorf("failed to unzip %s: %w", zp, err)
	}
	return NewModule(p)
}

// cachedModule returns a Module for mod if it's in either the local Go mod
// cache or apiviewgo cache. It returns errCachedModuleNotFound when the
// module isn't in either cache.
func cachedModule(mod module.Version) (*Module, error) {
	if modCache := os.Getenv("GOMODCACHE"); modCache != "" {
		d := filepath.Join(modCache, mustEscape(mod.Path)) + "@" + mod.Version
		if _, err := os.Stat(filepath.Join(d, "go.mod")); err == nil {
			return NewModule(d)
		}
	}
	return nil, errCachedModuleNotFound
}

// mustEscape escapes modPath. It panics when that fails.
func mustEscape(modPath string) string {
	escaped, err := module.EscapePath(modPath)
	if err != nil {
		panic(fmt.Errorf("failed to escape module path: %w", err))
	}
	return escaped
}

// downloadDir creates a directory to store downloaded module zips and source.
// Callers are responsible for removing the directory when they're done with it.
func downloadDir() (string, error) {
	root := os.TempDir()
	err := os.MkdirAll(root, 0700)
	if err != nil {
		return "", fmt.Errorf("failed to create root directory %q for downloads: %w", root, err)
	}
	d, err := os.MkdirTemp(root, "apiviewgo")
	if err != nil {
		err = fmt.Errorf("failed to create download directory %q: %w", d, err)
	}
	return d, err
}
