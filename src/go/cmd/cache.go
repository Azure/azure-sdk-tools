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

// GetExternalModule returns a Module representing mod. It looks for mod's source
// in the mod cache, when GOMODCACHE is set, and the apiviewgo cache. If GOMODCACHE
// isn't set or mod isn't in either cache, GetExternalModule downloads it from the
// Go module proxy and adds it to the apiviewgo cache but not GOMODCACHE.
func GetExternalModule(mod module.Version) (*Module, error) {
	m, err := cachedModule(mod)
	if err != nil && !errors.Is(err, errCachedModuleNotFound) {
		return nil, fmt.Errorf("failed to parse cached module %s: %w", mod.Path, err)
	}
	if m == nil {
		ctx, cancel := context.WithTimeout(context.Background(), time.Minute)
		defer cancel()
		if err = addModuleToCache(ctx, mod); err == nil {
			m, err = cachedModule(mod)
		}
	}
	return m, err
}

// addModuleToCache downloads mod from the Go module proxy and adds it to the local apiviewgo cache
func addModuleToCache(ctx context.Context, mod module.Version) error {
	escaped, err := module.EscapePath(mod.Path)
	if err != nil {
		return fmt.Errorf("unescapeable module path %q: %w", mod.Path, err)
	}
	u, err := url.Parse("https://" + path.Join("proxy.golang.org", escaped, "@v", mod.Version+".zip"))
	if err != nil {
		return fmt.Errorf("failed to parse module URL: %w", err)
	}
	req, err := http.NewRequestWithContext(ctx, http.MethodGet, u.String(), nil)
	if err != nil {
		return fmt.Errorf("failed to create request: %w", err)
	}
	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		return fmt.Errorf("failed to download module zip from %s: %w", u, err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		msg := "module proxy responded %d"
		if resp.Body != nil {
			b, _ := io.ReadAll(resp.Body)
			msg += ": " + string(b)
		}
		return fmt.Errorf(msg, resp.StatusCode)
	}
	zp := cachePathForZip(mod)
	d := filepath.Dir(zp)
	err = os.MkdirAll(d, 0700)
	if err != nil {
		return fmt.Errorf("failed to create %s: %w", d, err)
	}
	f, err := os.Create(zp)
	if err != nil {
		return fmt.Errorf("failed to create %s: %w", zp, err)
	}
	defer f.Close()
	_, err = io.Copy(f, resp.Body)
	if err != nil {
		return fmt.Errorf("failed to write %s: %w", zp, err)
	}
	err = zip.Unzip(cachePathForSource(mod), mod, zp)
	if err != nil {
		return fmt.Errorf("failed to unzip %s: %w", zp, err)
	}
	return nil
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
	d := cachePathForSource(mod)
	if _, err := os.Stat(filepath.Join(d, "go.mod")); err == nil {
		return NewModule(d)
	}
	zp := cachePathForZip(mod)
	if _, err := os.Stat(zp); err == nil {
		return modFromZip(mod)
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

// modFromZip unzips the module at mod's zip file in the local cache
func modFromZip(mod module.Version) (*Module, error) {
	d := cachePathForSource(mod)
	err := zip.Unzip(d, mod, cachePathForZip(mod))
	if err != nil {
		return nil, fmt.Errorf("failed to unzip module: %w", err)
	}
	if _, err := os.Stat(filepath.Join(d, "go.mod")); err != nil {
		return nil, fmt.Errorf("go.mod not found in %s", d)
	}
	return NewModule(d)
}

// the apiviewgo cache looks like:
//     /tmp/apiviewgo/cache
//     ├── azcore@v1.0.0
//     │   └── github.com/!azure/azure-sdk-for-go/sdk/azcore@v1.0.0
//     │       └── go.mod
//     └── zips
//         └── github.com/!azure/azure-sdk-for-go/sdk/azcore
//             └── v1.0.0.zip
//
// This is perhaps odd but zip.Unzip() requires the target directory be entirely
// empty, so obvious tidier schemes are impossible. Although we could in principle
// unzip modules to the local Go module cache, we don't do so because we don't
// want to affect other Go programs or to reimplement whatever `go mod download`
// behavior is necessary to ensure correctness in that cache.

func cacheDir() string {
	return filepath.Join(os.TempDir(), "apiviewgo", "cache")
}

// cachePathForSource returns the path to mod's directory in the local cache
func cachePathForSource(mod module.Version) string {
	return filepath.Join(cacheDir(), mustEscape(mod.Path)) + "@" + mod.Version
}

// cachePathForZip returns the path to mod's zip file in the local cache
func cachePathForZip(mod module.Version) string {
	return filepath.Join(cacheDir(), "zips", mustEscape(mod.Path), mod.Version+".zip")
}
