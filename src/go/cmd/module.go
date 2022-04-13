// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"fmt"
	"go/ast"
	"io/fs"
	"path/filepath"
	"strings"
)

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
		panic(dir + " isn't part of the Azure SDK for Go")
	}
	m := Module{Name: filepath.Base(dir), packages: map[string]*Pkg{}, path: dir}

	filepath.WalkDir(dir, func(path string, d fs.DirEntry, err error) error {
		if d.IsDir() {
			if strings.Contains(path, "testdata") {
				return filepath.SkipDir
			}
			p, err := NewPkg(path, m.Name)
			if err == nil {
				m.packages[baseImportPath+p.Name()] = p
			} else {
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
		for qn := range p.typeAliases {
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
							panic("couldn't load " + impPath)
						}
						pkg.Index()
						externalPackages[impPath] = pkg
						source = pkg
					}
				}
			}
			if source == nil {
				panic("haven't indexed " + impPath)
			}
			if def, ok := source.types[typeName]; ok {
				switch n := def.n.Type.(type) {
				case *ast.InterfaceType:
					p.c.addInterface(*def.p, def.n.Name.Name, n)
				case *ast.StructType:
					p.c.addStruct(*def.p, def.n.Name.Name, def.n)
				case *ast.Ident:
					p.c.addSimpleType(*p, def.n.Name.Name, def.n.Type.(*ast.Ident).Name)
				default:
					fmt.Printf("WARNING:  unexpected node type %T\n", def.n.Type)
				}
			} else {
				panic("no definition for " + qn)
			}
		}
	}
	return &m, nil
}
