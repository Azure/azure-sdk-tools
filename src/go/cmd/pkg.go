// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"errors"
	"fmt"
	"go/ast"
	"go/parser"
	"go/token"
	"io/ioutil"
	"os"
	"path/filepath"
	"strings"
)

// Pkg represents a Go package.
type Pkg struct {
	c          content
	files      map[string][]byte
	fs         *token.FileSet
	moduleName string
	p          *ast.Package
	path       string
	relName    string

	// typeAliases keys are type names defined in other packages that this package exports by alias.
	// For example, package "azcore" may export TokenCredential from azcore/internal/shared with
	// an alias like "type TokenCredential = shared.TokenCredential", in which case this map will
	// have key "azcore/internal/shared.TokenCredential". Values are meaningless--this is a map
	// only to facilitate identifying duplicates.
	typeAliases map[string]interface{}

	// types maps the name of a type defined in this package to that type's definition
	types map[string]typeDef
}

// NewPkg loads the package in the specified directory.
// It's required there is only one package in the directory.
func NewPkg(dir, moduleName string) (*Pkg, error) {
	pk := &Pkg{
		c:           newContent(),
		moduleName:  moduleName,
		path:        dir,
		typeAliases: map[string]interface{}{},
		types:       map[string]typeDef{},
	}
	if _, after, found := strings.Cut(dir, moduleName); found {
		pk.relName = moduleName
		if after != "" {
			pk.relName += after
		}
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
	if len(packages) != 1 {
		err = fmt.Errorf(`found %d packages in "%s"`, len(packages), dir)
		return nil, err
	}
	for name, p := range packages {
		// prune non-exported nodes
		if exp := ast.PackageExports(p); !exp {
			err = fmt.Errorf(`package "%s" exports nothing`, name)
			return nil, err
		}
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
	fmt.Println(p.path)
	for _, f := range p.p.Files {
		p.indexFile(f)
	}
}

func (p *Pkg) indexFile(f *ast.File) {
	// map import aliases to full import paths e.g. "shared" => "github.com/Azure/azure-sdk-for-go/sdk/azcore/internal/shared"
	sdkImports := map[string]string{}
	for _, imp := range f.Imports {
		// ignore third party and stdlib imports because we don't want to hoist their type definitions
		path := strings.Trim(imp.Path.Value, `"`)
		if strings.HasPrefix(path, "github.com/Azure/azure-sdk-for-go/sdk/") {
			sdkImports[filepath.Base(path)] = path
		}
	}

	ast.Inspect(f, func(n ast.Node) bool {
		switch x := n.(type) {
		case *ast.FuncDecl:
			p.c.addFunc(*p, x)
			// children can't be exported, let's not inspect them
			return false
		case *ast.GenDecl:
			if x.Tok == token.CONST || x.Tok == token.VAR {
				// const or var declaration
				kind := "const"
				if x.Tok == token.VAR {
					kind = "var  "
				}
				fmt.Printf("\t%s     %s\n", kind, x.Specs[0].(*ast.ValueSpec).Names[0])
				p.c.addGenDecl(*p, x)
			}
		case *ast.TypeSpec:
			switch t := x.Type.(type) {
			case *ast.ArrayType:
				// "type UUID [16]byte"
				txt := p.getText(t.Pos(), t.End())
				fmt.Printf("\ttype      %s %s\n", x.Name.Name, txt)
				p.types[x.Name.Name] = typeDef{name: x.Name.Name, n: x, p: p}
				p.c.addSimpleType(*p, x.Name.Name, txt)
			case *ast.FuncType:
				// "type PolicyFunc func(*Request) (*http.Response, error)"
				txt := p.getText(t.Pos(), t.End())
				fmt.Printf("\ttype      %s %s\n", x.Name.Name, txt)
				p.types[x.Name.Name] = typeDef{name: x.Name.Name, n: x, p: p}
				p.c.addSimpleType(*p, x.Name.Name, txt)
			case *ast.Ident:
				// "type ETag string"
				fmt.Printf("\ttype      %s %s\n", x.Name.Name, t.Name)
				p.types[x.Name.Name] = typeDef{name: x.Name.Name, n: x, p: p}
				p.c.addSimpleType(*p, x.Name.Name, t.Name)
			case *ast.InterfaceType:
				if _, ok := p.types[x.Name.Name]; ok {
					fmt.Printf("\tWARNING:  multiple definitions of '%s'\n", x.Name.Name)
				}
				p.types[x.Name.Name] = typeDef{name: x.Name.Name, n: x, p: p}
				fmt.Printf("\tinterface %s\n", x.Name.Name)
				p.c.addInterface(*p, x.Name.Name, t)
			case *ast.SelectorExpr:
				if ident, ok := t.X.(*ast.Ident); ok {
					if impPath, ok := sdkImports[ident.Name]; ok {
						// This is a re-exported SDK type e.g. "type TokenCredential = shared.TokenCredential".
						// Track it as an alias so we can later hoist its definition into this package.
						qn := impPath + "." + t.Sel.Name
						if _, ok := p.typeAliases[qn]; ok {
							fmt.Printf("\tWARNING:  multiple aliases for '%s'\n", qn)
						}
						p.typeAliases[qn] = x
						fmt.Printf("\talias     %s => %s\n", t.Sel.Name, qn)
					} else {
						// Non-SDK underlying type e.g. "type EDMDateTime time.Time". Handle it like a simple type
						// because we don't want to hoist its definition into this package.
						expr := p.getText(t.Pos(), t.End())
						fmt.Printf("\ttype      %s %s\n", x.Name.Name, expr)
						p.c.addSimpleType(*p, x.Name.Name, expr)
					}
				}
			case *ast.StructType:
				fmt.Printf("\tstruct    %s\n", x.Name.Name)
				if _, ok := p.types[x.Name.Name]; ok {
					fmt.Printf("\tWARNING:  multiple definitions of '%s'\n", x.Name.Name)
				}
				p.types[x.Name.Name] = typeDef{name: x.Name.Name, n: x, p: p}
				p.c.addStruct(*p, x.Name.Name, x)
			default:
				txt := p.getText(x.Pos(), x.End())
				fmt.Printf("\tWARNING:  unexpected node type %T: %s\n", t, txt)
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
		b, err := ioutil.ReadFile(p.Filename)
		if err != nil {
			panic(err)
		}
		pkg.files[p.Filename] = b
	}
	return string(pkg.files[p.Filename][p.Offset : p.Offset+int(end-start)])
}

// creates a Func object from the specified ast.FuncType
func (pkg Pkg) buildFunc(ft *ast.FuncType) Func {
	f := Func{}

	if ft.TypeParams != nil {
		f.TypeParams = make([]string, 0, len(ft.TypeParams.List))
		pkg.translateFieldList(ft.TypeParams.List, func(param *string, constraint string) {
			// constraint == "" when the parameter has no constraint
			f.TypeParams = append(f.TypeParams, strings.TrimRight(*param+" "+constraint, " "))
		})
	}

	if ft.Params.List != nil {
		f.Params = make([]string, 0, len(ft.Params.List))
		pkg.translateFieldList(ft.Params.List, func(n *string, t string) {
			p := ""
			if n != nil {
				p = *n + " "
			}
			p += t
			f.Params = append(f.Params, p)
		})
	}

	if ft.Results != nil {
		f.Returns = make([]string, 0, len(ft.Results.List))
		pkg.translateFieldList(ft.Results.List, func(n *string, t string) {
			f.Returns = append(f.Returns, t)
		})
	}
	return f
}

// iterates over the specified field list, for each field the specified
// callback is invoked with the name of the field and the type name.  the field
// name can be nil, e.g. anonymous fields in structs, unnamed return types etc.
func (pkg Pkg) translateFieldList(fl []*ast.Field, cb func(*string, string)) {
	for _, f := range fl {
		t := pkg.getText(f.Type.Pos(), f.Type.End())
		if len(f.Names) == 0 {
			// field is an unnamed func return
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

type typeDef struct {
	name string
	// n is the AST node defining the type
	n *ast.TypeSpec
	// p is the package defining the type
	p *Pkg
}
