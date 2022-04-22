// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"errors"
	"fmt"
	"go/ast"
	"io/fs"
	"path/filepath"
	"strings"
)

// indexTestdata allows tests to index this module's testdata
// (we never want to do that for real reviews)
var indexTestdata bool

// Module collects the data required to describe an Azure SDK module's public API.
type Module struct {
	Name string

	// packages maps import paths to packages
	packages map[string]*Pkg

	// path is the module's path on disk
	path string
}

// NewModule indexes an Azure SDK module's ASTs. dir must be under azure-sdk-for-go/sdk.
func NewModule(dir string) (*Module, error) {
	var baseSDKPath, baseImportPath string
	if before, _, found := strings.Cut(dir, "sdk"+string(filepath.Separator)); found {
		baseSDKPath = filepath.Join(before, "sdk")
		baseImportPath = "github.com/Azure/azure-sdk-for-go/sdk/"
	} else {
		fmt.Println(dir + " isn't part of the Azure SDK for Go. Output may be incomplete or inaccurate.")
	}
	m := Module{Name: filepath.Base(dir), packages: map[string]*Pkg{}, path: dir}

	filepath.WalkDir(dir, func(path string, d fs.DirEntry, err error) error {
		if d.IsDir() {
			if !indexTestdata && strings.Contains(path, "testdata") {
				return filepath.SkipDir
			}
			p, err := NewPkg(path, m.Name)
			if err == nil {
				m.packages[baseImportPath+p.Name()] = p
			} else if !errors.Is(err, ErrNoPackages) {
				fmt.Printf("error: %v\n", err)
			}
		}
		return nil
	})

	for _, p := range m.packages {
		p.Index()
	}

	// Add the definitions of types exported by alias to each package's content. For example,
	// given "type TokenCredential = shared.TokenCredential" in package azcore, this will hoist
	// the definition from azcore/internal/shared into the APIView for azcore, making the type's
	// fields visible there.
	externalPackages := map[string]*Pkg{}
	for _, p := range m.packages {
		for alias, qn := range p.typeAliases {
			// qn is a type name qualified with import path like
			// "github.com/Azure/azure-sdk-for-go/sdk/azcore/internal/shared.TokenRequestOptions"
			impPath := qn[:strings.LastIndex(qn, ".")]
			typeName := qn[len(impPath)+1:]
			var source *Pkg
			var ok bool
			if source, ok = m.packages[impPath]; !ok {
				// must be a package external to this module
				if source, ok = externalPackages[impPath]; !ok {
					// figure out a path to the package, index it
					if _, after, found := strings.Cut(impPath, "azure-sdk-for-go/sdk/"); found {
						path := filepath.Join(baseSDKPath, after)
						pkg, err := NewPkg(path, after)
						if err != nil {
							fmt.Printf("couldn't parse %s: %v", impPath, err)
							continue
						}
						pkg.Index()
						externalPackages[impPath] = pkg
						source = pkg
					}
				}
			}

			var t TokenMaker
			if source == nil {
				t = p.c.addSimpleType(*p, alias, p.Name(), qn)
			} else if def, ok := source.types[typeName]; ok {
				switch n := def.n.Type.(type) {
				case *ast.InterfaceType:
					t = p.c.addInterface(*def.p, alias, p.Name(), n)
				case *ast.StructType:
					t = p.c.addStruct(*def.p, alias, p.Name(), def.n)
				case *ast.Ident:
					t = p.c.addSimpleType(*p, alias, p.Name(), def.n.Type.(*ast.Ident).Name)
				default:
					fmt.Printf("unexpected node type %T", def.n.Type)
				}
			} else {
				fmt.Println("found no definition for " + qn)
			}
			if t != nil {
				path := strings.TrimPrefix(qn, baseImportPath)
				level := DiagnosticLevelInfo
				if !strings.Contains(path, m.Name) {
					// this type is defined in another module
					level = DiagnosticLevelWarning
				}
				p.diagnostics = append(p.diagnostics, Diagnostic{
					Level:    level,
					TargetID: t.ID(),
					Text:     aliasFor + path,
				})
			}
		}
	}
	return &m, nil
}
