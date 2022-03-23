// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"fmt"
	"go/ast"
	"go/parser"
	"go/token"
	"io/ioutil"
	"os"
	"strings"
)

// Pkg represents a Go package.
type Pkg struct {
	c       content
	files   map[string][]byte
	fs      *token.FileSet
	p       *ast.Package
	relName string

	// typeAliases contains type names defined in other packages that this package exports by alias.
	// For example, package "azcore" may export TokenCredential from azcore/internal/shared with
	// an alias like "type TokenCredential = shared.TokenCredential", in which case this map will
	// have key "azcore/internal/shared.TokenCredential". The value is meaningless--this is a map
	// only to facilitate identifying duplicates.
	typeAliases map[string]interface{}
}

// NewPkg loads the package in the specified directory.
// It's required there is only one package in the directory.
func NewPkg(dir string) (*Pkg, error) {
	pk := &Pkg{c: newContent(), typeAliases: map[string]interface{}{}}
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

// Name returns the pkg name.
func (pkg Pkg) Name() string {
	return pkg.p.Name
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
func (pkg Pkg) buildFunc(ft *ast.FuncType) (f Func) {
	// appends a to s, comma-delimited style, and returns s
	appendString := func(s, a string) string {
		if s != "" {
			s += ","
		}
		s += a
		return s
	}

	// build the params type list
	if ft.Params.List != nil {
		p := ""
		pkg.translateFieldList(ft.Params.List, func(n *string, t string) {
			temp := ""
			if n != nil {
				temp = *n + " "
			}
			temp += t
			p = appendString(p, temp)
		})
		f.Params = &p
	}

	// build the return types list
	if ft.Results != nil {
		r := ""
		pkg.translateFieldList(ft.Results.List, func(n *string, t string) {
			r = appendString(r, t)
		})
		f.Returns = &r

		f.ReturnsNum = len(ft.Results.List)
	}
	return
}

// iterates over the specified field list, for each field the specified
// callback is invoked with the name of the field and the type name.  the field
// name can be nil, e.g. anonymous fields in structs, unnamed return types etc.
func (pkg Pkg) translateFieldList(fl []*ast.Field, cb func(*string, string)) {
	for _, f := range fl {
		var name *string
		if f.Names != nil {
			n := pkg.getText(f.Names[0].Pos(), f.Names[0].End())
			name = &n
		}
		t := pkg.getText(f.Type.Pos(), f.Type.End())
		cb(name, t)
	}
}
