// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"errors"
	"fmt"
	"go/ast"
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

// sdkDirName allows tests to set the name of the assumed common
// directory of all Azure SDK modules, which enables tests to
// pass without the code below having to compute this directory
var sdkDirName = "sdk"

// versionReg is the regex for version part in import
var versionReg = regexp.MustCompile(`/v\d+$|/v\d+/`)

// Module collects the data required to describe an Azure SDK module's public API.
type Module struct {
	Name string
	// PackageName is the name of the APIView review for this module
	PackageName string

	// packages maps import paths to packages
	packages map[string]*Pkg
}

// NewModule indexes an Azure SDK module's ASTs
func NewModule(dir string) (*Module, error) {
	mf, err := parseModFile(dir)
	if err != nil {
		return nil, err
	}
	// sdkRoot is the path on disk to the sdk folder e.g. /home/user/me/azure-sdk-for-go/sdk.
	// Used to find definitions of types imported from other Azure SDK modules.
	sdkRoot := ""
	packageName := ""
	if before, after, found := strings.Cut(dir, fmt.Sprintf("%s%c", sdkDirName, filepath.Separator)); found {
		sdkRoot = filepath.Join(before, sdkDirName)
		if filepath.Base(after) == "internal" {
			packageName = after
		} else {
			packageName = filepath.Base(after)
		}
		fmt.Printf("Package Name: %s\n", packageName)
	}

	//Package name can still be empty when generating API review using uploaded zip folder of a specific package in which case parent directory will not be sdk
	if packageName == "" {
		modulePath := mf.Module.Mod.Path
		packageName = path.Base(modulePath)
		fmt.Printf("Module path: %s\n", modulePath)
		// Set relative path as package name for internal package to avoid collision
		if packageName == "internal" {
			if _, after, found := strings.Cut(modulePath, fmt.Sprintf("/%s/", sdkDirName)); found {
				packageName = after
			}
		}
		fmt.Printf("Package Name: %s\n", packageName)
	}
	m := Module{Name: filepath.Base(dir), PackageName: packageName, packages: map[string]*Pkg{}}

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
			p, err := NewPkg(path, mf.Module.Mod.Path)
			if err == nil {
				m.packages[baseImportPath+p.Name()] = p
			} else if !errors.Is(err, ErrNoPackages) {
				return err
			}
		}
		return nil
	})
	if err != nil {
		return nil, err
	}

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
				if source, ok = externalPackages[impPath]; !ok && sdkRoot != "" {
					// figure out a path to the package, index it
					if _, after, found := strings.Cut(impPath, "azure-sdk-for-go/sdk/"); found {
						p := filepath.Join(sdkRoot, strings.TrimSuffix(versionReg.ReplaceAllString(after, "/"), "/"))
						pkg, err := NewPkg(p, "github.com/Azure/azure-sdk-for-go/sdk/"+after)
						if err == nil {
							pkg.Index()
							externalPackages[impPath] = pkg
							source = pkg
						} else {
							// types from this module will appear in the review without their definitions
							fmt.Printf("couldn't parse %s: %v\n", impPath, err)
						}
					}
				}
			}

			level := DiagnosticLevelInfo
			originalName := qn
			if _, after, found := strings.Cut(qn, m.Name); found {
				originalName = strings.TrimPrefix(after, "/")
			} else {
				// this type is defined in another module
				level = DiagnosticLevelWarning
			}

			var t TokenMaker
			if source == nil {
				t = p.c.addSimpleType(*p, alias, p.Name(), originalName, nil)
			} else if def, ok := recursiveFindTypeDef(typeName, source, m.packages); ok {
				switch n := def.n.Type.(type) {
				case *ast.InterfaceType:
					t = p.c.addInterface(*def.p, alias, p.Name(), n, nil)
				case *ast.StructType:
					t = p.c.addStruct(*def.p, alias, p.Name(), def.n, nil)
					hoistMethodsForType(source, alias, p)
					// ensure that all struct field types that are structs are also aliased from this package
					for _, field := range n.Fields.List {
						fieldTypeName := unwrapStructFieldTypeName(field)
						if fieldTypeName == "" {
							// we can ignore this field
							continue
						}

						// ensure that our package exports this type
						if _, ok := p.typeAliases[fieldTypeName]; ok {
							// found an alias
							continue
						}

						// no alias, add a diagnostic
						p.diagnostics = append(p.diagnostics, Diagnostic{
							Level:    DiagnosticLevelError,
							TargetID: t.ID(),
							Text:     missingAliasFor + fieldTypeName,
						})
					}
				case *ast.Ident:
					t = p.c.addSimpleType(*p, alias, p.Name(), def.n.Type.(*ast.Ident).Name, nil)
					hoistMethodsForType(source, alias, p)
				default:
					fmt.Printf("unexpected node type %T\n", def.n.Type)
					t = p.c.addSimpleType(*p, alias, p.Name(), originalName, nil)
				}
			} else {
				fmt.Println("found no definition for " + qn)
			}

			if t != nil {
				p.diagnostics = append(p.diagnostics, Diagnostic{
					Level:    level,
					TargetID: t.ID(),
					Text:     aliasFor + originalName,
				})
			}
		}
	}
	return &m, nil
}

// returns the type name for the specified struct field.
// if the field can be ignored, an empty string is returned.
func unwrapStructFieldTypeName(field *ast.Field) string {
	if field.Names != nil && !field.Names[0].IsExported() {
		// field isn't exported so skip it
		return ""
	}

	// start with the field expression
	exp := field.Type

	// if it's an array, get the element expression.
	// current codegen doesn't support *[]Type so no need to handle it.
	if at, ok := exp.(*ast.ArrayType); ok {
		// FieldName []FieldType
		// FieldName []*FieldType
		exp = at.Elt
	}

	// from here we either have a pointer-to-type or type
	var ident *ast.Ident
	if se, ok := exp.(*ast.StarExpr); ok {
		// FieldName *FieldType
		ident, _ = se.X.(*ast.Ident)
	} else {
		// FieldName FieldType
		ident, _ = exp.(*ast.Ident)
	}

	// !IsExported() is a hacky way to ignore primitive types
	// FieldName bool
	if ident == nil || !ident.IsExported() {
		return ""
	}

	// returns FieldType
	return ident.Name
}

func hoistMethodsForType(pkg *Pkg, typeName string, target *Pkg) {
	methods := pkg.c.findMethods(typeName)
	for sig, fn := range methods {
		target.c.Funcs[sig] = fn.ForAlias(target.Name())
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

func recursiveFindTypeDef(typeName string, source *Pkg, packages map[string]*Pkg) (typeDef, bool) {
	def, ok := source.types[typeName]
	if ok {
		return def, true
	}

	// this is a type alias.  recursively find its typeDef
	alias, ok := source.typeAliases[typeName]
	if ok {
		// alias == github.com/Azure/azure-sdk-for-go/sdk/storage/azblob/container.DeleteOptions
		split := strings.LastIndex(alias, ".")
		if split < 0 {
			return typeDef{}, false
		}

		pkgName := alias[:split]   // github.com/Azure/azure-sdk-for-go/sdk/storage/azblob/container
		typeName = alias[split+1:] // DeleteOptions
		split = strings.LastIndex(pkgName, "/")
		if split < 0 {
			return typeDef{}, false
		}

		pkgName = pkgName[split+1:] // container
		source = nil
		for _, pkg := range packages {
			if pkg.p.Name == pkgName {
				source = pkg
				break
			}
		}
		if source == nil {
			return typeDef{}, false
		}

		return recursiveFindTypeDef(typeName, source, packages)
	}

	return typeDef{}, false
}
