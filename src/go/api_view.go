// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package apiview

import (
	"encoding/json"
	"fmt"
	"go/ast"
	"go/parser"
	"go/token"
	"io/ioutil"
)

const (
	text          = 0
	newline       = 1
	whitespace    = 2
	punctuation   = 3
	keyword       = 4
	lineIDMarker  = 5
	typeName      = 6
	memberName    = 7
	stringLiteral = 8
	literal       = 9
	comment       = 10
)

var (
	reservedNames = map[string]bool{
		"string":     true,
		"byte":       true,
		"int":        true,
		"int8":       true,
		"int16":      true,
		"int32":      true,
		"int64":      true,
		"float32":    true,
		"float64":    true,
		"rune":       true,
		"bool":       true,
		"map":        true,
		"uint":       true,
		"uint8":      true,
		"uint16":     true,
		"uint32":     true,
		"uint64":     true,
		"complex64":  true,
		"complex128": true,
	}
)

// CreateAPIView generates the output file that the API view tool uses.
func CreateAPIView(pkgDir, outputDir string) error {
	// load the given Go package
	pkg, err := loadPackage(pkgDir)
	if err != nil {
		return err
	}
	tokenList := &[]Token{}
	c := newContent()
	c = inspectAST(pkg)
	c.parseConst(tokenList)
	c.parseInterface(tokenList)
	c.parseStruct(tokenList)
	c.parseFunc(tokenList)
	navItems := c.generateNavChildItems()
	review := PackageReview{
		Name:   pkg.p.Name,
		Tokens: *tokenList,
		Navigation: []Navigation{
			{
				Text:       &pkg.p.Name,
				ChildItems: navItems,
			},
		},
	}
	filename := fmt.Sprintf("%s%s.json", outputDir, pkg.p.Name)
	file, _ := json.MarshalIndent(review, "", " ")
	_ = ioutil.WriteFile(filename, file, 0644)
	return nil
}

// get loads the package in the specified directory and returns the exported
// content.  It's a convenience wrapper around LoadPackage() and GetExports().
func get(pkgDir string) (content, error) {
	pkg, err := loadPackage(pkgDir)
	if err != nil {
		return content{}, err
	}
	return pkg.GetExports(), nil
}

// inspectAST is used to traverse the Go AST and accumulate interface, struct, func and const definitions.
func inspectAST(pkg pkg) content {
	c := newContent()
	ast.Inspect(pkg.p, func(n ast.Node) bool {
		switch x := n.(type) {
		case *ast.TypeSpec:
			if t, ok := x.Type.(*ast.StructType); ok {
				c.addStruct(pkg, x.Name.Name, t)
			} else if t, ok := x.Type.(*ast.InterfaceType); ok {
				c.addInterface(pkg, x.Name.Name, t)
			}
		case *ast.FuncDecl:
			c.addFunc(pkg, x)
			// return false as we don't care about the function body.
			// this is super important as it filters out the majority of
			// the package's AST making it WAY easier to find the bits
			// of interest (not doing this will break a lot of code).
			return false
		case *ast.GenDecl:
			if x.Tok == token.CONST {
				c.addConst(pkg, x)
			}
		}
		return true
	})

	return c
}

// loadPackage loads the package in the specified directory.
// It's required there is only one package in the directory.
func loadPackage(dir string) (pkg pkg, err error) {
	pkg.files = map[string][]byte{}
	pkg.f = token.NewFileSet()
	packages, err := parser.ParseDir(pkg.f, dir, nil, 0)
	// packages, err := parser.ParseFile(pkg.f, dir, nil, 0)
	if err != nil {
		return
	}
	if len(packages) < 1 {
		err = fmt.Errorf("didn't find any packages in '%s'. Length: %d", dir, len(packages))
		return
	}
	if len(packages) > 1 {
		err = fmt.Errorf("found more than one package in '%s'. Length: %d", dir, len(packages))
		return
	}
	for pn := range packages {
		p := packages[pn]
		// trim any non-exported nodes
		if exp := ast.PackageExports(p); !exp {
			err = fmt.Errorf("package '%s' doesn't contain any exports", pn)
			return
		}
		pkg.p = p
		return
	}
	// shouldn't ever get here...
	panic("failed to return package")
}

// GetExports returns the exported content of the package.
func (pkg pkg) GetExports() (c content) {
	c = newContent()
	ast.Inspect(pkg.p, func(n ast.Node) bool {
		switch x := n.(type) {
		case *ast.TypeSpec:
			if t, ok := x.Type.(*ast.StructType); ok {
				c.addStruct(pkg, x.Name.Name, t)
			} else if t, ok := x.Type.(*ast.InterfaceType); ok {
				c.addInterface(pkg, x.Name.Name, t)
			}
		case *ast.FuncDecl:
			c.addFunc(pkg, x)
			// return false as we don't care about the function body.
			// this is super important as it filters out the majority of
			// the package's AST making it WAY easier to find the bits
			// of interest (not doing this will break a lot of code).
			return false
		case *ast.GenDecl:
			if x.Tok == token.CONST {
				c.addConst(pkg, x)
			}
		}
		return true
	})
	return
}

// Name returns the pkg name.
func (pkg pkg) Name() string {
	return pkg.p.Name
}

// returns the text between [start, end]
func (pkg pkg) getText(start token.Pos, end token.Pos) string {
	// convert to absolute position within the containing file
	p := pkg.f.Position(start)
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
func (pkg pkg) buildFunc(ft *ast.FuncType) (f Func) {
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
func (pkg pkg) translateFieldList(fl []*ast.Field, cb func(*string, string)) {
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
