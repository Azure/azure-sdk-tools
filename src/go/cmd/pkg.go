// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"errors"
	"fmt"
	"go/ast"
	"go/parser"
	"go/token"
	"os"
	"os/exec"
	"path"
	"path/filepath"
	"strings"
	"unicode"

	"golang.org/x/exp/slices"
	"golang.org/x/mod/module"
)

// diagnostic messages
const (
	aliasFor               = "Alias for "
	missingAliasFor        = "missing alias for nested type "
	embedsUnexportedStruct = "Anonymously embeds unexported struct "
	sealedInterface        = "Applications can't implement this interface"
)

var ErrNoPackages = errors.New("no packages found")

// Pkg represents a Go package.
type Pkg struct {
	modulePath     string
	moduleRoot     string
	c              content
	diagnostics    []CodeDiagnostic
	files          map[string][]byte
	fs             *token.FileSet
	p              *ast.Package
	relName        string
	modulePackages map[string]*Pkg
	packageNames   map[string]string

	// TypeAliases are types exported from this package but defined in another. For
	// example, package "azcore" may export TokenCredential from azcore/internal/shared
	// with an alias like "type TokenCredential = shared.TokenCredential". This slice
	// holds pointers so Module can update aliases with data not available to Pkg such
	// as the source module path.
	TypeAliases []*TypeAlias

	// types maps the name of a type defined in this package to that type's definition
	types map[string]typeDef
}

// NewPkg loads the package in the specified directory.
//
//   - dir is the directory containing the package
//   - modulePath is the import path of the module containing the package
//   - moduleRoot is the root directory of the module on disk i.e., the directory containing its go.mod
func NewPkg(dir, modulePath, moduleRoot string) (*Pkg, error) {
	// ensure that all directories are using the same path separator
	// character else the below call to strings.Cut() will fail
	dir = filepath.ToSlash(dir)
	moduleRoot = filepath.ToSlash(moduleRoot)

	pk := &Pkg{
		modulePath:  modulePath,
		moduleRoot:  moduleRoot,
		c:           newContent(),
		diagnostics: []CodeDiagnostic{},
		types:       map[string]typeDef{},
	}
	modulePathWithoutVersion := strings.TrimSuffix(versionReg.ReplaceAllString(modulePath, "/"), "/")
	moduleName := filepath.Base(modulePathWithoutVersion)
	if _, after, found := strings.Cut(dir, moduleRoot); found {
		pk.relName = strings.ReplaceAll(moduleName+after, "\\", "/")
	} else {
		return nil, errors.New(dir + " isn't part of module " + moduleName)
	}
	pk.files = map[string][]byte{}
	pk.fs = token.NewFileSet()
	packages, err := parser.ParseDir(pk.fs, dir, func(f os.FileInfo) bool {
		// exclude test files
		return !strings.HasSuffix(f.Name(), "_test.go")
	}, 0)
	if err != nil {
		return nil, err
	}
	if ps := len(packages); ps != 1 {
		err = ErrNoPackages
		if ps > 1 {
			err = fmt.Errorf(`found %d packages in "%s"`, ps, dir)
		}
		return nil, err
	}
	for _, p := range packages {
		pk.p = p
		return pk, nil
	}
	// shouldn't ever get here...
	panic("failed to load package")
}

// Name returns the package's name relative to its module, for example "azcore/runtime".
func (pkg Pkg) Name() string {
	return pkg.relName
}

// Index parses the package's files, adding exported types to the package's content as discovered.
func (p *Pkg) Index() {
	for _, f := range p.p.Files {
		p.indexFile(f)
	}
}

// returns p.modulePath minus any major version suffix
func (p *Pkg) unversionedModulePath() string {
	if versionReg.MatchString(p.modulePath) {
		return path.Dir(p.modulePath)
	}
	return p.modulePath
}

func unparenType(expr ast.Expr) ast.Expr {
	for {
		paren, ok := expr.(*ast.ParenExpr)
		if !ok {
			return expr
		}
		expr = paren.X
	}
}

func (p *Pkg) indexFile(f *ast.File) {
	// map import aliases to full import paths e.g. "shared" => "github.com/Azure/azure-sdk-for-go/sdk/azcore/internal/shared"
	imports := map[string]string{}
	modulePathReplacement := ""
	if versionReg.MatchString(p.modulePath) {
		// the module has a major version suffix. we need to remove that from the
		// sub-package's path. the reason being that we index packages without the
		// major version suffix (see module.go@NewModule) to avoid noise in the
		// diff across major versions. so, if we don't also remove it here, we will
		// fail to look up cross package references (e.g. type aliases)
		modulePathReplacement = path.Dir(p.modulePath)
	}
	for _, imp := range f.Imports {
		pkgPath := strings.Trim(imp.Path.Value, `"`)
		if modulePathReplacement != "" {
			pkgPath = strings.ReplaceAll(pkgPath, p.modulePath, modulePathReplacement)
		}
		if imp.Name != nil {
			imports[imp.Name.String()] = pkgPath
		} else {
			name := filepath.Base(pkgPath)
			if resolved, err := p.packageName(pkgPath); err == nil {
				name = resolved
			}
			imports[name] = pkgPath
		}
	}

	ast.Inspect(f, func(n ast.Node) bool {
		switch x := n.(type) {
		case *ast.FuncDecl:
			p.c.addFunc(*p, x, imports)
			// children can't be exported, let's not inspect them
			return false
		case *ast.GenDecl:
			if x.Tok == token.CONST || x.Tok == token.VAR {
				// const or var declaration
				for _, s := range x.Specs {
					p.c.addGenDecl(*p, x.Tok, s.(*ast.ValueSpec), imports)
				}
			}
		case *ast.TypeSpec:
			switch t := unparenType(x.Type).(type) {
			case *ast.ArrayType:
				// "type UUID [16]byte"
				txt := p.getText(t.Pos(), t.End())
				p.types[x.Name.Name] = typeDef{n: x, p: p, imports: imports}
				p.c.addSimpleType(*p, x.Name.Name, p.Name(), txt, imports)
			case *ast.ChanType, *ast.StarExpr:
				txt := p.getText(t.Pos(), t.End())
				p.types[x.Name.Name] = typeDef{n: x, p: p, imports: imports}
				p.c.addSimpleType(*p, x.Name.Name, p.Name(), txt, imports)
			case *ast.FuncType:
				// "type PolicyFunc func(*Request) (*http.Response, error)"
				txt := p.getText(t.Pos(), t.End())
				p.types[x.Name.Name] = typeDef{n: x, p: p, imports: imports}
				p.c.addSimpleType(*p, x.Name.Name, p.Name(), txt, imports)
			case *ast.Ident:
				// "type ETag string"
				p.types[x.Name.Name] = typeDef{n: x, p: p, imports: imports}
				p.c.addSimpleType(*p, x.Name.Name, p.Name(), t.Name, imports)
			case *ast.IndexExpr, *ast.IndexListExpr:
				// "type Client GenericClient[BaseClient]"
				// "type Client CompositeClient[BaseClient1, BaseClient2]"
				txt := p.getText(t.Pos(), t.End())
				p.types[x.Name.Name] = typeDef{n: x, p: p, imports: imports}
				p.c.addSimpleType(*p, x.Name.Name, p.Name(), txt, imports)
			case *ast.InterfaceType:
				p.types[x.Name.Name] = typeDef{n: x, p: p, imports: imports}
				in := p.c.addInterface(*p, x.Name.Name, p.Name(), x, imports)
				if in.Sealed {
					p.diagnostics = append(p.diagnostics, CodeDiagnostic{
						TargetID: in.ID(),
						Level:    CodeDiagnosticLevelInfo,
						Text:     sealedInterface,
					})
				}
			case *ast.MapType:
				// "type opValues map[reflect.Type]interface{}"
				txt := p.getText(t.Pos(), t.End())
				p.types[x.Name.Name] = typeDef{n: x, p: p, imports: imports}
				p.c.addSimpleType(*p, x.Name.Name, p.Name(), txt, imports)
			case *ast.SelectorExpr:
				if ident, ok := t.X.(*ast.Ident); ok {
					if impPath, ok := imports[ident.Name]; ok {
						// alias in the same module could use type navigator directly
						if _, _, found := strings.Cut(impPath, p.unversionedModulePath()); found && !strings.Contains(impPath, "internal") {
							expr := p.getText(t.Pos(), t.End())
							p.c.addSimpleType(*p, x.Name.Name, p.Name(), expr, imports)
						}

						// This is a re-exported type e.g. "type TokenCredential = shared.TokenCredential".
						// Track it as an alias so we can later hoist its definition into this package.
						ta := TypeAlias{
							Name:          x.Name.Name,
							Package:       p,
							QualifiedName: impPath + "." + t.Sel.Name,
							isAlias:       x.Assign.IsValid(),
						}
						p.TypeAliases = append(p.TypeAliases, &ta)
					} else {
						// Non-SDK underlying type e.g. "type EDMDateTime time.Time". Handle it like a simple type
						// because we don't want to hoist its definition into this package.
						expr := p.getText(t.Pos(), t.End())
						p.c.addSimpleType(*p, x.Name.Name, p.Name(), expr, imports)
					}
				}
			case *ast.StructType:
				p.types[x.Name.Name] = typeDef{n: x, p: p, imports: imports}
				s := p.c.addStruct(*p, x.Name.Name, p.Name(), x, imports)
				for _, t := range s.AnonymousFields {
					// if t contains "." it must be exported
					if !strings.Contains(t, ".") && unicode.IsLower(rune(t[0])) {
						p.diagnostics = append(p.diagnostics, CodeDiagnostic{
							Level:    CodeDiagnosticLevelError,
							TargetID: s.ID(),
							Text:     embedsUnexportedStruct + t,
						})
					}
				}
			default:
				txt := p.getText(x.Pos(), x.End())
				fmt.Printf("unhandled node type %T: %s\n", t, txt)
			}
			p.c.setTypeAlias(x.Name.Name, x.Assign.IsValid())
		}
		return true
	})
}

func (p *Pkg) packageName(importPath string) (string, error) {
	if name, ok := p.packageNames[importPath]; ok {
		return name, nil
	}
	if imported, ok := p.modulePackages[importPath]; ok && imported.p != nil {
		return imported.p.Name, nil
	}
	cmd := exec.Command("go", "list", "-mod=readonly", "-f", "{{.Name}}", importPath)
	cmd.Dir = filepath.FromSlash(p.moduleRoot)
	output, err := cmd.CombinedOutput()
	if err != nil {
		return "", fmt.Errorf("resolving Go package name for %s: %w: %s", importPath, err, strings.TrimSpace(string(output)))
	}
	name := strings.TrimSpace(string(output))
	if name == "" || strings.ContainsAny(name, "\r\n") {
		return "", fmt.Errorf("go list returned an invalid package name for %s", importPath)
	}
	if p.packageNames != nil {
		p.packageNames[importPath] = name
	}
	return name, nil
}

// returns the text between [start, end]
func (pkg Pkg) getText(start token.Pos, end token.Pos) string {
	// convert to absolute position within the containing file
	p := pkg.fs.Position(start)
	// check if the file has been loaded, if not then load it
	if _, ok := pkg.files[p.Filename]; !ok {
		b, err := os.ReadFile(p.Filename)
		if err != nil {
			panic(err)
		}
		pkg.files[p.Filename] = b
	}
	return string(pkg.files[p.Filename][p.Offset : p.Offset+int(end-start)])
}

// iterates over the specified field list, for each field the specified
// callback is invoked with the name of the field and the type name.  the field
// name can be nil, e.g. anonymous fields in structs, unnamed return types etc.
func (pkg Pkg) translateFieldList(fl []*ast.Field, cb func(*string, string)) {
	for _, f := range fl {
		t := pkg.getText(f.Type.Pos(), f.Type.End())
		if len(f.Names) == 0 {
			// field is an unnamed func return or anonymously embedded
			cb(nil, t)
		}
		// field could have multiple names: in "type A struct { m, n int }",
		// syntactically speaking, A has one field having two names
		for _, name := range f.Names {
			n := pkg.getText(name.Pos(), name.End())
			cb(&n, t)
		}
	}
}

// translateType change type string and add navigator mark:
// 1. type in the same package or module, add navigator prefix <navigator> to the type string
// 2. type in different module or system type, do nothing
func (pkg Pkg) translateType(oriVal string, imports map[string]string) string {
	now := ""
	result := ""
	for _, ch := range oriVal {
		switch string(ch) {
		case "*", "[", "]", " ", "\t", "\r", "\n", ";", "(", ")", "{", "}", ",":
			if now != "" {
				result += pkg.addTypeNavigator(now, imports)
				now = ""
			}
			result += string(ch)
		case ".":
			if now == ".." {
				result += "..."
				now = ""
			} else {
				now += "."
			}
		default:
			now += string(ch)
		}
	}
	if now != "" {
		result += pkg.addTypeNavigator(now, imports)
	}
	return result
}

func (pkg Pkg) addTypeNavigator(oriVal string, imports map[string]string) string {
	switch {
	case slices.Contains(keywords, oriVal) || slices.Contains(internalTypes, oriVal):
		return oriVal
	default:
		splits := strings.Split(oriVal, ".")
		if len(splits) == 1 {
			return fmt.Sprintf("<%s.%s>%s", pkg.Name(), oriVal, oriVal)
		} else {
			// find exact import path of a type
			if impPath, ok := imports[splits[0]]; ok {
				// judge if import path is in the module
				if _, after, found := strings.Cut(impPath, pkg.unversionedModulePath()); found {
					return fmt.Sprintf("<%s.%s>%s", path.Base(pkg.unversionedModulePath())+after, splits[1], oriVal)
				}
			}
			return oriVal
		}
	}
}

// TypeAlias represents a type exported from one package but defined in another. In code
// this looks like "type Event = log.Event".
type TypeAlias struct {
	// Name in the exporting package e.g. "Event"
	Name string
	// Package containing the alias
	Package *Pkg
	// QualifiedName of the source type e.g. "github.com/Azure/azure-sdk-for-go/sdk/internal/log.Event"
	QualifiedName string
	// SourceMod is the module defining the type
	SourceMod module.Version

	// isAlias distinguishes an alias from a new type with the same underlying type.
	isAlias bool
	// resolved indicates whether the alias has been resolved
	resolved bool
}

// Resolve adds review content for the alias. If def is nonzero i.e., it carries a syntax node for the type definition,
// Resolve adds that definition to the package exporting the alias. Otherwise, Resolve adds a SimpleType representing the
// alias to the review.
func (a *TypeAlias) Resolve(def typeDef) error {
	if a.resolved {
		// this should never happen but if it does, it's a bug we want to know about
		return fmt.Errorf("alias %s already resolved", a.Name)
	}
	if def.n != nil && def.p != nil {
		a.cacheDefinition(def)
	}
	// Private aliases can be used by public aliases, but have no review line of their own.
	if !ast.IsExported(a.Name) {
		a.resolved = true
		return nil
	}
	level := CodeDiagnosticLevelInfo
	originalName := a.QualifiedName
	// if the definition is in the same module as the alias, strip the module path from the diagnostic message
	if _, after, found := strings.Cut(a.QualifiedName, a.Package.unversionedModulePath()); found {
		// after is e.g. "/internal/log.Event" or ".StatusType"
		originalName = after[1:]
	} else {
		level = CodeDiagnosticLevelWarning
	}
	// Index() may have recorded the alias as a SimpleType we're about to replace with something more
	// detailed, so we remove that SimpleType to avoid displaying it as a duplicate type in the review
	delete(a.Package.c.SimpleTypes, a.Name)
	var t TokenMaker
	if def.n == nil || def.p == nil {
		t = a.Package.c.addSimpleType(*a.Package, a.Name, a.Package.Name(), a.QualifiedName, nil)
	} else {
		switch n := unparenType(def.n.Type).(type) {
		case *ast.InterfaceType:
			t = a.Package.c.addInterface(*def.p, a.Name, a.Package.Name(), def.n, nil)
		case *ast.StructType:
			t = a.Package.c.addStruct(*def.p, a.Name, a.Package.Name(), def.n, nil)
			if err := a.hoistMethods(def); err != nil {
				return err
			}
			// ensure that all struct field types that are structs are also aliased from this package
			for _, field := range n.Fields.List {
				if def.p == a.Package {
					break
				}
				fieldTypeName := unwrapStructFieldTypeName(field)
				if fieldTypeName == "" {
					// we can ignore this field
					continue
				}

				// ensure that our package exports this type
				found := false
				for _, ta := range a.Package.TypeAliases {
					if ta.Name == fieldTypeName {
						found = true
						break
					}
				}
				if found {
					continue
				}

				// no alias, add a diagnostic
				a.Package.diagnostics = append(a.Package.diagnostics, CodeDiagnostic{
					Level:    CodeDiagnosticLevelError,
					TargetID: t.ID(),
					Text:     missingAliasFor + fieldTypeName,
				})
			}
		case *ast.Ident:
			t = a.Package.c.addSimpleType(*a.Package, a.Name, a.Package.Name(), n.Name, nil)
			if err := a.hoistMethods(def); err != nil {
				return err
			}
		case *ast.StarExpr:
			if a.isAlias {
				return fmt.Errorf("unsupported type %T for alias %s.%s", n, a.Package.Name(), a.Name)
			}
			underlying, err := a.compositeType(def, n)
			if err != nil {
				return err
			}
			simple := NewSimpleType(a.Name, a.Package.Name(), underlying)
			a.Package.c.SimpleTypes[a.Name] = simple
			t = simple
		case *ast.ArrayType, *ast.ChanType, *ast.FuncType, *ast.MapType:
			underlying, err := a.compositeType(def, n)
			if err != nil {
				return err
			}
			simple := NewSimpleType(a.Name, a.Package.Name(), underlying)
			a.Package.c.SimpleTypes[a.Name] = simple
			t = simple
			if err := a.hoistMethods(def); err != nil {
				return err
			}
		default:
			return fmt.Errorf("unsupported type %T for alias %s.%s", n, a.Package.Name(), a.Name)
		}
	}

	if t != nil {
		a.Package.c.setTypeAlias(a.Name, a.isAlias)
		if a.isAlias && def.p != nil && def.p != a.Package &&
			ast.IsExported(a.QualifiedName[strings.LastIndex(a.QualifiedName, ".")+1:]) {
			a.Package.c.setTypeAliasTarget(a.Name, a.QualifiedName)
		}
		a.Package.diagnostics = append(a.Package.diagnostics, CodeDiagnostic{
			Level:    level,
			TargetID: t.ID(),
			Text:     aliasFor + originalName,
		})
	}
	a.resolved = true
	return nil
}

func (a *TypeAlias) cacheDefinition(def typeDef) {
	if !a.isAlias {
		def.methodPackage = a.Package
		def.methodName = a.Name
	}
	a.Package.types[a.Name] = def
}

func (a *TypeAlias) hoistMethods(def typeDef) error {
	if !a.isAlias || !ast.IsExported(a.Name) {
		return nil
	}
	pkg, name := def.p, def.n.Name.Name
	if def.methodPackage != nil {
		pkg, name = def.methodPackage, def.methodName
	}
	return hoistMethodsForType(pkg, name, a.Package, a.Name)
}

// TODO: could be replaced by TokenMaker
type typeDef struct {
	// n is the AST node defining the type
	n *ast.TypeSpec
	// p is the package defining the type
	p *Pkg
	// methodPackage and methodName identify a defined type whose underlying type is n.
	methodPackage *Pkg
	methodName    string
	imports       map[string]string
}
