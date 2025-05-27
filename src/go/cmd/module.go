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
	"golang.org/x/mod/module"
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
	// ModFile is the parsed go.mod file for the module
	ModFile *modfile.File
	// Name of the module's root package e.g. "azcore"
	Name string
	// Packages maps import paths to the module's Packages
	Packages map[string]*Pkg
}

// getPackageNameFromModPath gets the API review name for the module at modPath
func getPackageNameFromModPath(modPath string) string {
	// for official SDKs, use a subset of the full module path
	// e.g. github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/appcomplianceautomation/armappcomplianceautomation
	// becomes sdk/resourcemanager/appcomplianceautomation/armappcomplianceautomation
	if suffix, ok := strings.CutPrefix(modPath, "github.com/Azure/azure-sdk-for-go/"); ok {
		modPath = suffix
	}
	// now strip off any major version suffix
	if loc := regexp.MustCompile(`/v\d+$`).FindStringIndex(modPath); loc != nil {
		modPath = modPath[:loc[0]]
	}
	return modPath
}

// NewModule indexes a module's ASTs
func NewModule(dir string) (*Module, error) {
	fmt.Println("Indexing", dir)
	mf, err := parseModFile(dir)
	if err != nil {
		return nil, err
	}
	name := filepath.Base(dir)
	if before, _, found := strings.Cut(name, "@"); found {
		// dir is in the module cache, something like "/home/me/go/pkg/mod/github.com/Foo/bar@v1.0.0"
		name = before
	}
	m := Module{
		ModFile:  mf,
		Name:     name,
		Packages: map[string]*Pkg{},
	}

	baseImportPath := path.Dir(m.ModFile.Module.Mod.Path) + "/"
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
			p, err := NewPkg(path, m.ModFile.Module.Mod.Path, dir)
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

	for _, p := range m.Packages {
		p.Index()
	}
	// resolve cross-package references by adding the definitions of types exported by alias to the exporting package
	for _, p := range m.Packages {
		for _, alias := range p.TypeAliases {
			if def, ok := recursiveFindTypeDef(alias.Name, p, m.Packages); ok {
				alias.Resolve(def)
				continue
			}
			// The definition is in another module. Add the alias to
			// ExternalAliases so the caller can find the definition later.
			for _, req := range m.ModFile.Require {
				if strings.HasPrefix(alias.QualifiedName, req.Mod.Path) {
					alias.SourceMod = req.Mod
					break
				}
			}
			if alias.SourceMod == (module.Version{}) {
				// The exporting module doesn't require the source module, so this must be a standard library type.
				// We want this to appear in the API like "type AzureTime time.Time" and don't want to hoist the
				// definition into the review. Resolving with a zero typeDef adds a SimpleType to the review.
				alias.Resolve(typeDef{})
			} else {
				m.ExternalAliases = append(m.ExternalAliases, alias)
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

// recursiveFindTypeDef finds a type definition in a collection of packages.
//
//   - typeName is the unqualified name of the type e.g. "RetryOptions"
//   - source is the package containing the type alias
//   - packages is the collection of Pkgs to search. Its keys are import paths e.g.
//     "github.com/Azure/azure-sdk-for-go/sdk/storage/azblob/container"
func recursiveFindTypeDef(typeName string, source *Pkg, packages map[string]*Pkg) (typeDef, bool) {
	def, ok := source.types[typeName]
	if ok {
		return def, true
	}
	// source doesn't define typeName; it must export typeName via an alias.
	// Recurse into the package from which source imports typeName.
	for _, a := range source.TypeAliases {
		if a.Name == typeName {
			// a.QualifiedName == github.com/Azure/azure-sdk-for-go/sdk/storage/azblob/container.DeleteOptions
			dot := strings.LastIndex(a.QualifiedName, ".")
			if len(a.QualifiedName)-2 < dot || dot < 1 {
				// there must be at least one rune before and after the dot
				panic(fmt.Sprintf("alias %q refers to an invalid qualified name %q", a.Name, a.QualifiedName))
			}
			pkgPath := a.QualifiedName[:dot]      // github.com/Azure/azure-sdk-for-go/sdk/storage/azblob/container
			sourceName := a.QualifiedName[dot+1:] // DeleteOptions
			if p, ok := packages[pkgPath]; ok {
				return recursiveFindTypeDef(sourceName, p, packages)
			}
			break
		}
	}
	return typeDef{}, false
}
