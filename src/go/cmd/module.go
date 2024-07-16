// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"errors"
	"io/fs"
	"os"
	"path"
	"path/filepath"
	"regexp"
	"strings"

	"golang.org/x/mod/modfile"
)

// indexTestdata allows tests to index this module's testdata
// (we never want to do that for real reviews)
var indexTestdata bool

// versionReg is the regex for version part in import
var versionReg = regexp.MustCompile(`/v\d+$|/v\d+/`)

// Module collects the data required to describe an Azure SDK module's public API.
type Module struct {
	// ExternalAliases are type aliases referring to other modules
	ExternalAliases []*TypeAlias
	Name string
	// Packages maps import paths to the module's Packages
	Packages map[string]*Pkg
}

// NewModule constructs a Module, locating its constituent packages but not parsing any source.
// Call [Module.Index] to collect type information.
func NewModule(dir string) (*Module, error) {
	mf, err := parseModFile(dir)
	if err != nil {
		return nil, err
	}
	m := Module{
		Name:        filepath.Base(dir),
		PackageName: getPackageNameFromModPath(mf.Module.Mod.Path),
		Packages:    map[string]*Pkg{},
	}
	fmt.Printf("Package Name: %s\n", m.PackageName)

	baseImportPath := path.Dir(mf.Module.Mod.Path) + "/"
	if baseImportPath == "./" {
		// this is a relative path in the tests, so remove this prefix.
		// if not, then the package name added below won't match the imported packages.
		baseImportPath = ""
	}
	err = filepath.WalkDir(dir, func(path string, d fs.DirEntry, err error) error {
		if d.IsDir() {
			if !indexTestdata && strings.Contains(path, "testdata") {
				return filepath.SkipDir
			}
			if path != dir {
				// This is a subdirectory of the module we're indexing. If it contains
				// a go.mod, this subdirectory contains a separate module, not a package
				// of the module we're indexing.
				if _, err := os.Stat(filepath.Join(path, "go.mod")); err == nil {
					return filepath.SkipDir
				}
			}
			p, err := NewPkg(path, mf.Module.Mod.Path)
			if err == nil {
				m.Packages[baseImportPath+p.Name()] = p
			} else if !errors.Is(err, ErrNoPackages) {
				return err
			}
		}
		return nil
	})
	if err != nil {
		return nil, err
	}
	return &m, nil
}

// Index all the types in the module's packages, populating m.Packages and resolving
// cross-package type aliases. It adds cross-module aliases to [Module.ExternalAliases]
// so callers can later resolve these after indexing referenced external modules.
func (m *Module) Index() {
	for _, p := range m.Packages {
		p.Index()
	}

	// resolve cross-package references by adding the definitions of types exported by alias to the exporting package
	for _, p := range m.Packages {
		for _, alias := range p.TypeAliases {
			if def, ok := recursiveFindTypeDef(alias, p, m.Packages); ok {
				alias.Resolve(&def)
				continue
			}
			// The definition is in another module. Add the alias to
			// ExternalAliases so the caller can find the definition later.
			for _, req := range m.ModFile.Require {
				if strings.HasPrefix(alias.QualifiedName, req.Mod.Path) {
					alias.SourceModPath = req.Mod.Path
					break
				}
			}
			m.ExternalAliases = append(m.ExternalAliases, alias)
		}
	}
}

func parseModFile(dir string) (*modfile.File, error) {
	p := filepath.Join(dir, "go.mod")
	content, err := os.ReadFile(p)
	if err != nil {
		return nil, err
	}
	return modfile.Parse(p, content, nil)
}
