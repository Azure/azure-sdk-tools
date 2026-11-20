// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"bufio"
	"errors"
	"fmt"
	"go/ast"
	"io/fs"
	"os"
	"path"
	"path/filepath"
	"regexp"
	"sort"
	"strings"

	"golang.org/x/mod/modfile"
	"golang.org/x/mod/module"
)

// indexTestdata allows tests to index this module's testdata
// (we never want to do that for real reviews)
var indexTestdata bool

// versionReg is the regex for version part in import
var versionReg = regexp.MustCompile(`/v\d+$|/v\d+/`)

// used to find the semver const definition. the definition
// is pretty standard so this is easier than using the ast,
// plus the constant isn't exported
var moduleVerReg = regexp.MustCompile(`^\s*(?:const\s+)?[A-Za-z_][A-Za-z0-9_]*\s*=\s*"v(?P<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?)"\s*$`)

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
	// Version is the semantic version of the module without the leading "v", e.g. 1.2.3
	Version string
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
	return newModule(dir, false, dir)
}

func NewModuleForDependency(dir string) (*Module, error) {
	return newModule(dir, true, dir)
}

func newModuleForDependencyInContext(dir, packageNameRoot string) (*Module, error) {
	return newModule(dir, true, packageNameRoot)
}

func newModule(dir string, deferAliases bool, packageNameRoot string) (*Module, error) {
	fmt.Println("Indexing", dir)
	if packageNameRoot == "" {
		packageNameRoot = dir
	}
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

	// baseImportPath contains the module identity *minus* the module's root package.
	// e.g. for module github.com/foo/bar the value will be github.com/foo/
	// the package name is concatenated to this value after its creation.
	var baseImportPath string
	if versionReg.MatchString(m.ModFile.Module.Mod.Path) {
		// we need to strip off the version suffix AND the package name
		baseImportPath = path.Dir(path.Dir(m.ModFile.Module.Mod.Path))
	} else {
		baseImportPath = path.Dir(m.ModFile.Module.Mod.Path)
	}
	baseImportPath += "/"
	if baseImportPath == "./" {
		// this is a relative path in the tests, so remove this prefix.
		// if not, then the package name added below won't match the imported packages.
		baseImportPath = ""
	}
	err = filepath.WalkDir(dir, func(path string, d fs.DirEntry, _ error) error {
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
				p.packageNameRoot = filepath.ToSlash(packageNameRoot)
				m.Packages[baseImportPath+p.Name()] = p
			} else if !errors.Is(err, ErrNoPackages) {
				return err
			}
		} else if fileName := d.Name(); strings.Contains(fileName, "constant") || strings.Contains(fileName, "version") {
			// find the constant that contains the semver
			// e.g.
			// moduleVersion = "v1.2.0"
			// Version = "v1.21.1-beta.1"
			f, err := os.Open(path)
			if err != nil {
				return err
			}
			scanner := bufio.NewScanner(f)
			for scanner.Scan() {
				if match := moduleVerReg.FindStringSubmatch(scanner.Text()); match != nil {
					matched := match[moduleVerReg.SubexpIndex("version")]
					if m.Version != "" {
						return fmt.Errorf("multiple semver matches, found %s when version is already %s", matched, m.Version)
					}
					m.Version = matched
					break
				}
			}
			if err := scanner.Err(); err != nil {
				return err
			}
			if err := f.Close(); err != nil {
				return err
			}
		}
		return nil
	})
	if err != nil {
		return nil, err
	}

	packageNames := make(map[string]string, len(m.Packages))
	for importPath, p := range m.Packages {
		p.modulePackages = m.Packages
		packageNames[importPath] = p.p.Name
	}
	for _, p := range m.Packages {
		p.packageNames = packageNames
	}
	for _, p := range m.Packages {
		if err := p.Index(); err != nil {
			return nil, fmt.Errorf("indexing package %s: %w", p.Name(), err)
		}
	}
	// resolve cross-package references by adding the definitions of types exported by alias to the exporting package
	for _, p := range m.Packages {
		for _, alias := range p.TypeAliases {
			def, found, err := findTypeDef(alias.Name, p, m.Packages, map[typeDefRef]bool{}, true)
			alias.SourceMod = sourceModuleForAlias(alias.QualifiedName, m.ModFile.Require)
			if deferAliases {
				if alias.SourceMod != (module.Version{}) {
					m.ExternalAliases = append(m.ExternalAliases, alias)
				}
				continue
			}
			if alias.SourceMod != (module.Version{}) {
				m.ExternalAliases = append(m.ExternalAliases, alias)
				continue
			}
			if strings.Contains(p.relName, "/internal") {
				if err == nil && found {
					alias.cacheDefinition(def)
				}
				continue
			}
			if !alias.isAlias && alias.definition.n != nil && def.n == alias.definition.n && def.underlyingType == "" {
				dot := strings.LastIndex(alias.QualifiedName, ".")
				if dot > 0 {
					importPath := alias.QualifiedName[:dot]
					modulePath := p.unversionedModulePath()
					if importPath == modulePath || strings.HasPrefix(importPath, modulePath+"/") {
						alias.SourceMod = module.Version{Path: m.ModFile.Module.Mod.Path}
						m.ExternalAliases = append(m.ExternalAliases, alias)
						continue
					}
				}
			}
			if err != nil {
				return nil, fmt.Errorf("resolving alias %s.%s: %w", p.Name(), alias.Name, err)
			}
			if found {
				if err := alias.Resolve(def); err != nil {
					return nil, fmt.Errorf("resolving alias %s.%s: %w", p.Name(), alias.Name, err)
				}
				continue
			}
			// The definition is in another module. Add the alias to
			// ExternalAliases so the caller can find the definition later.
			if alias.SourceMod == (module.Version{}) {
				dot := strings.LastIndex(alias.QualifiedName, ".")
				if dot > 0 {
					importPath := alias.QualifiedName[:dot]
					if strings.HasPrefix(importPath, p.unversionedModulePath()+"/") &&
						(strings.HasSuffix(importPath, "/internal") || strings.Contains(importPath, "/internal/")) {
						alias.SourceMod = module.Version{Path: m.ModFile.Module.Mod.Path}
						m.ExternalAliases = append(m.ExternalAliases, alias)
						continue
					}
				}
				// The exporting module doesn't require the source module, so this must be a standard library type.
				// We want this to appear in the API like "type AzureTime time.Time" and don't want to hoist the
				// definition into the review. Resolving with a zero typeDef adds a SimpleType to the review.
				if err := alias.Resolve(typeDef{}); err != nil {
					return nil, fmt.Errorf("resolving alias %s.%s: %w", p.Name(), alias.Name, err)
				}
			} else {
				m.ExternalAliases = append(m.ExternalAliases, alias)
			}
		}
	}
	return &m, nil
}

func sourceModuleForAlias(qualifiedName string, requirements []*modfile.Require) module.Version {
	var best module.Version
	for _, requirement := range requirements {
		if (qualifiedName == requirement.Mod.Path ||
			strings.HasPrefix(qualifiedName, requirement.Mod.Path+".") ||
			strings.HasPrefix(qualifiedName, requirement.Mod.Path+"/")) &&
			len(requirement.Mod.Path) > len(best.Path) {
			best = requirement.Mod
		}
	}
	return best
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

func hoistMethodsForType(pkg *Pkg, typeName string, target *Pkg, aliasName string) error {
	names := pkg.methodReceiverNames(typeName)
	if pkg == target && typeName == aliasName && len(names) == 1 {
		return nil
	}
	receivers := make(map[string]bool, len(names))
	for _, name := range names {
		receivers[name] = true
	}
	type receiverMethod struct {
		receiver string
		fn       Func
	}
	methods := []receiverMethod{}
	for _, fn := range pkg.c.Funcs {
		receiver := fn.receiverTypeName()
		if receivers[receiver] && ast.IsExported(fn.Name()) {
			if err := validateCompositeType(fn.rawType, pkg, target, fn.imports); err != nil {
				return fmt.Errorf("unsupported method signature %s.%s: %w", receiver, fn.Name(), err)
			}
			if pkg == target && receiver == aliasName {
				continue
			}
			methods = append(methods, receiverMethod{receiver, fn})
		}
	}
	for _, method := range methods {
		alias := method.fn.ForAlias(pkg, target, method.receiver, aliasName, names)
		target.c.Funcs[alias.signature()] = alias
	}
	return nil
}

func (p *Pkg) methodReceiverNames(typeName string) []string {
	// Aliases share a method set; newly defined types form a separate boundary.
	canonical := p.methodReceiverType(typeName)
	names := []string{typeName}
	for name, def := range p.types {
		if name == typeName || (def.methodPackage != nil && def.methodPackage != p) ||
			(def.methodPackage == nil && def.p != p) {
			continue
		}
		if p.methodReceiverType(name) == canonical {
			names = append(names, name)
		}
	}
	sort.Strings(names)
	return names
}

func (p *Pkg) methodReceiverType(name string) string {
	for remaining := len(p.types) + 1; remaining > 0; remaining-- {
		def, ok := p.types[name]
		if !ok || def.n == nil {
			break
		}
		if def.methodPackage != nil {
			if def.methodPackage != p || def.methodName == name {
				break
			}
			name = def.methodName
		} else if def.p != p {
			break
		} else if def.n.Name.Name != name {
			name = def.n.Name.Name
		} else if def.n.Assign.IsValid() {
			if ident, ok := unparenType(def.n.Type).(*ast.Ident); ok {
				name = ident.Name
			} else {
				break
			}
		} else {
			break
		}
	}
	return name
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
func recursiveFindTypeDef(typeName string, source *Pkg, packages map[string]*Pkg) (typeDef, bool, error) {
	return findTypeDef(typeName, source, packages, map[typeDefRef]bool{}, true)
}

type typeDefRef struct {
	pkg  *Pkg
	name string
}

func findTypeDef(typeName string, source *Pkg, packages map[string]*Pkg, seen map[typeDefRef]bool, preserveDefinedTypes bool) (typeDef, bool, error) {
	ref := typeDefRef{pkg: source, name: typeName}
	if seen[ref] {
		return typeDef{}, false, fmt.Errorf("cyclic type definition %s.%s", source.Name(), typeName)
	}
	seen[ref] = true
	if preserveDefinedTypes {
		for _, alias := range source.TypeAliases {
			if alias.Name == typeName && !alias.isAlias && alias.definition.n != nil {
				return resolveDefinedSelector(alias.definition, source, packages, seen)
			}
		}
	}
	def, ok := source.types[typeName]
	if ok {
		if def.p == source && def.n.Name.Name == typeName {
			if preserveDefinedTypes && !def.n.Assign.IsValid() {
				if _, ok := unparenType(def.n.Type).(*ast.SelectorExpr); ok {
					return resolveDefinedSelector(def, source, packages, seen)
				}
			}
			if ident, ok := unparenType(def.n.Type).(*ast.Ident); ok {
				underlying, found, err := findTypeDef(ident.Name, source, packages, seen, preserveDefinedTypes)
				if err != nil {
					return typeDef{}, false, err
				}
				if found {
					if !def.n.Assign.IsValid() {
						// A defined type owns its methods even when its underlying type is elsewhere.
						underlying.methodPackage = source
						underlying.methodName = typeName
					}
					return underlying, true, nil
				}
			}
		}
		return def, true, nil
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
				def, found, err := findTypeDef(sourceName, p, packages, seen, preserveDefinedTypes)
				if found && !a.isAlias {
					def.methodPackage = source
					def.methodName = a.Name
				}
				return def, found, err
			}
			break
		}
	}
	return typeDef{}, false, nil
}

func resolveDefinedSelector(def typeDef, source *Pkg, packages map[string]*Pkg, seen map[typeDefRef]bool) (typeDef, bool, error) {
	def.methodPackage = source
	def.methodName = def.n.Name.Name
	selector, ok := unparenType(def.n.Type).(*ast.SelectorExpr)
	if !ok {
		return def, true, nil
	}
	name, ok := selector.X.(*ast.Ident)
	if !ok {
		return typeDef{}, false, fmt.Errorf("unsupported selector in type %s", def.n.Name.Name)
	}
	importPath, ok := def.imports[name.Name]
	if !ok {
		return typeDef{}, false, fmt.Errorf("unknown import name %s in type %s", name.Name, def.n.Name.Name)
	}
	qualifiedName := importPath + "." + selector.Sel.Name
	if pkg, ok := packages[importPath]; ok {
		resolved, found, err := findTypeDef(selector.Sel.Name, pkg, packages, seen, true)
		if err != nil {
			return typeDef{}, false, err
		}
		if !found {
			return def, true, nil
		}
		resolved.methodPackage = source
		resolved.methodName = def.n.Name.Name
		return resolved, true, nil
	}
	if isStandardLibraryImportPath(importPath) {
		def.underlyingType = qualifiedName
	}
	return def, true, nil
}
