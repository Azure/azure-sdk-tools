// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"errors"
	"fmt"
	"go/ast"
	"go/parser"
	"go/token"
	"golang.org/x/exp/slices"
	"os"
	"path"
	"path/filepath"
	"strings"
	"unicode"
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
	modulePath  string
	c           content
	diagnostics []Diagnostic
	files       map[string][]byte
	fs          *token.FileSet
	p           *ast.Package
	relName     string

	// typeAliases keys are the names of types defined in other packages which this package exports by alias.
	// For example, package "azcore" may export TokenCredential from azcore/internal/shared with
	// an alias like "type TokenCredential = shared.TokenCredential", in which case this map will
	// have key "TokenCredential" with value "azcore/internal/shared.TokenCredential"
	typeAliases map[string]string

	// types maps the name of a type defined in this package to that type's definition
	types map[string]typeDef
}

// NewPkg loads the package in the specified directory.
// It's required there is only one package in the directory.
func NewPkg(dir, modulePath string) (*Pkg, error) {
	pk := &Pkg{
		modulePath:  modulePath,
		c:           newContent(),
		diagnostics: []Diagnostic{},
		typeAliases: map[string]string{},
		types:       map[string]typeDef{},
	}
	modulePathWithoutVersion := strings.TrimSuffix(versionReg.ReplaceAllString(modulePath, "/"), "/")
	moduleName := filepath.Base(modulePathWithoutVersion)
	if _, after, found := strings.Cut(dir, moduleName); found {
		pk.relName = moduleName
		if after != "" {
			pk.relName += after
		}
		pk.relName = strings.ReplaceAll(pk.relName, "\\", "/")
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

func (p *Pkg) indexFile(f *ast.File) {
	// map import aliases to full import paths e.g. "shared" => "github.com/Azure/azure-sdk-for-go/sdk/azcore/internal/shared"
	imports := map[string]string{}
	for _, imp := range f.Imports {
		p := strings.Trim(imp.Path.Value, `"`)
		if imp.Name != nil {
			imports[imp.Name.String()] = p
		} else {
			imports[filepath.Base(p)] = p
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
			switch t := x.Type.(type) {
			case *ast.ArrayType:
				// "type UUID [16]byte"
				txt := p.getText(t.Pos(), t.End())
				p.types[x.Name.Name] = typeDef{n: x, p: p}
				p.c.addSimpleType(*p, x.Name.Name, p.Name(), txt, imports)
			case *ast.FuncType:
				// "type PolicyFunc func(*Request) (*http.Response, error)"
				txt := p.getText(t.Pos(), t.End())
				p.types[x.Name.Name] = typeDef{n: x, p: p}
				p.c.addSimpleType(*p, x.Name.Name, p.Name(), txt, imports)
			case *ast.Ident:
				// "type ETag string"
				p.types[x.Name.Name] = typeDef{n: x, p: p}
				p.c.addSimpleType(*p, x.Name.Name, p.Name(), t.Name, imports)
			case *ast.IndexExpr, *ast.IndexListExpr:
				// "type Client GenericClient[BaseClient]"
				// "type Client CompositeClient[BaseClient1, BaseClient2]"
				txt := p.getText(t.Pos(), t.End())
				p.types[x.Name.Name] = typeDef{n: x, p: p}
				p.c.addSimpleType(*p, x.Name.Name, p.Name(), txt, imports)
			case *ast.InterfaceType:
				p.types[x.Name.Name] = typeDef{n: x, p: p}
				in := p.c.addInterface(*p, x.Name.Name, p.Name(), t, imports)
				if in.Sealed {
					p.diagnostics = append(p.diagnostics, Diagnostic{
						TargetID: in.ID(),
						Level:    DiagnosticLevelInfo,
						Text:     sealedInterface,
					})
				}
			case *ast.MapType:
				// "type opValues map[reflect.Type]interface{}"
				txt := p.getText(t.Pos(), t.End())
				p.c.addSimpleType(*p, x.Name.Name, p.Name(), txt, imports)
			case *ast.SelectorExpr:
				if ident, ok := t.X.(*ast.Ident); ok {
					if impPath, ok := imports[ident.Name]; ok {
						// alias in the same module could use type navigator directly
						if _, _, found := strings.Cut(impPath, p.modulePath); found && !strings.Contains(impPath, "internal") {
							expr := p.getText(t.Pos(), t.End())
							p.c.addSimpleType(*p, x.Name.Name, p.Name(), expr, imports)
						}

						// This is a re-exported type e.g. "type TokenCredential = shared.TokenCredential".
						// Track it as an alias so we can later hoist its definition into this package.
						qn := impPath + "." + t.Sel.Name
						p.typeAliases[x.Name.Name] = qn
					} else {
						// Non-SDK underlying type e.g. "type EDMDateTime time.Time". Handle it like a simple type
						// because we don't want to hoist its definition into this package.
						expr := p.getText(t.Pos(), t.End())
						p.c.addSimpleType(*p, x.Name.Name, p.Name(), expr, imports)
					}
				}
			case *ast.StructType:
				p.types[x.Name.Name] = typeDef{n: x, p: p}
				s := p.c.addStruct(*p, x.Name.Name, p.Name(), x, imports)
				for _, t := range s.AnonymousFields {
					// if t contains "." it must be exported
					if !strings.Contains(t, ".") && unicode.IsLower(rune(t[0])) {
						p.diagnostics = append(p.diagnostics, Diagnostic{
							Level:    DiagnosticLevelError,
							TargetID: s.ID(),
							Text:     embedsUnexportedStruct + t,
						})
					}
				}
			default:
				txt := p.getText(x.Pos(), x.End())
				fmt.Printf("unhandled node type %T: %s\n", t, txt)
			}
		}
		return true
	})
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
		case "*", "[", "]", " ", "(", ")", "{", "}", ",":
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
				now = now + "."
			}
		default:
			now = now + string(ch)
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
				if _, after, found := strings.Cut(impPath, pkg.modulePath); found {
					return fmt.Sprintf("<%s.%s>%s", path.Base(pkg.modulePath)+after, splits[1], oriVal)
				}
			}
			return oriVal
		}
	}
}

// TODO: could be replaced by TokenMaker
type typeDef struct {
	// n is the AST node defining the type
	n *ast.TypeSpec
	// p is the package defining the type
	p *Pkg
}
