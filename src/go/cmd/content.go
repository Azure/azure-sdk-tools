// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"fmt"
	"go/ast"
	"go/token"
	"regexp"
	"slices"
	"sort"
	"strings"
	"unicode"
)

// skip adding the const type in the token list
const skip = "Untyped const"

// content defines the set of exported constants, funcs, and structs.
type content struct {
	// the list of exported constants.
	// key is the exported name, value is its type and value.
	Consts map[string]Declaration

	// Funcs maps exported function signatures (including any receiver) to definition data
	Funcs map[string]Func

	// the list of exported interfaces.
	// key is the exported name, value contains the interface definition.
	Interfaces map[string]Interface `json:"interfaces,omitempty"`

	// SimpleTypes are types with underlying types other than interface and struct, for example "type Thing string"
	SimpleTypes map[string]SimpleType

	// the list of exported struct types.
	// key is the exported name, value contains field information.
	Structs map[string]Struct `json:"structs,omitempty"`

	Vars map[string]Declaration
}

// newContent returns an initialized Content object.
func newContent() content {
	return content{
		Consts:      make(map[string]Declaration),
		Funcs:       make(map[string]Func),
		Interfaces:  make(map[string]Interface),
		SimpleTypes: make(map[string]SimpleType),
		Structs:     make(map[string]Struct),
		Vars:        make(map[string]Declaration),
	}
}

// isEmpty returns true if there is no content in any of the fields.
func (c content) isEmpty() bool {
	return len(c.Consts)+len(c.Funcs)+len(c.Interfaces)+len(c.SimpleTypes)+len(c.Structs)+len(c.Vars) == 0
}

// addGenDecl adds const and var declaration to the exports list
// The imports map stores the key value pair for package imports which will be used to identify types.
func (c *content) addGenDecl(pkg Pkg, tok token.Token, vs *ast.ValueSpec, imports map[string]string) Declaration {
	if len(vs.Values) > 0 {
		v := getExprValue(pkg, vs.Values[0])
		if v == "" {
			fmt.Println("failed to determine value for " + pkg.getText(vs.Pos(), vs.End()))
		}
	}
	decl := NewDeclaration(pkg, vs, imports)
	// TODO handle multiple names like "var a, b = 42"
	switch tok {
	case token.CONST:
		c.Consts[vs.Names[0].Name] = decl
	case token.VAR:
		c.Vars[vs.Names[0].Name] = decl
	default:
		fmt.Printf("unexpected declaration kind %v\n", vs.Names[0].Obj.Kind)
	}
	return decl
}

func getExprValue(pkg Pkg, expr ast.Expr) string {
	switch x := expr.(type) {
	case *ast.BasicLit:
		// const DefaultLinkCredit = 1
		return x.Value
	case *ast.BinaryExpr:
		// const FooConst = "value" + Bar
		return pkg.getText(x.X.Pos(), x.Y.End())
	case *ast.CallExpr:
		// const FooConst = Foo("value")
		// var Foo = NewFoo()
		return pkg.getText(x.Pos(), x.End())
	case *ast.CompositeLit:
		// var AzureChina = Configuration{ LoginEndpoint: "https://login.chinacloudapi.cn/", Services: map[ServiceName]ServiceConfiguration{} }
		txt := pkg.getText(expr.Pos(), expr.End())
		return txt
	case *ast.FuncLit:
		// TODO: if we ever have a public one of these, choose a better way to format it
		txt := pkg.getText(expr.Pos(), expr.End())
		return txt
	case *ast.Ident:
		// const DefaultLinkBatching = false
		return x.Name
	case *ast.SelectorExpr:
		// const ModeUnsettled = encoding.ModeUnsettled
		return pkg.getText(x.Pos(), x.End())
	case *ast.UnaryExpr:
		// const FooConst = -1
		return pkg.getText(x.Pos(), x.End())
	default:
		fmt.Printf("unhandled expression value type %T\n", expr)
		txt := pkg.getText(expr.Pos(), expr.End())
		return txt
	}
}

func includesType(s []string, t string) bool {
	for _, j := range s {
		if j == t {
			return true
		}
	}
	return false
}

func (c *content) parseSimpleType() []ReviewLine {
	lns := []ReviewLine{}
	keys := make([]string, 0, len(c.SimpleTypes))
	for name := range c.SimpleTypes {
		if unicode.IsUpper(rune(name[0])) {
			keys = append(keys, name)
		}
	}
	sort.Strings(keys)
	for _, name := range keys {
		t := c.SimpleTypes[name]
		ln := ReviewLine{
			Children: c.searchForMethods(t.Name()),
			LineID:   t.ID(),
			Tokens:   t.MakeTokens(),
		}
		if len(ln.Children) > 0 {
			ln.Children = append(ln.Children, ReviewLine{})
		}
		if consts := c.filterDeclarations(t.Name(), c.Consts); len(consts) > 0 {
			ln.Children = append(ln.Children, c.parseDeclarations(consts, "const")...)
		}
		if vars := c.filterDeclarations(t.Name(), c.Vars); len(vars) > 0 {
			ln.Children = append(ln.Children, c.parseDeclarations(vars, "var")...)
		}
		lns = append(lns, ln)
		if len(ln.Children) == 0 {
			lns = append(lns, ReviewLine{IsContextEndLine: true})
		}
	}
	return lns
}

func (c *content) parseConst() []ReviewLine {
	return c.parseDeclarations(c.Consts, "const")
}

func (c *content) parseVar() []ReviewLine {
	return c.parseDeclarations(c.Vars, "var")
}

func (c *content) parseDeclarations(decls map[string]Declaration, kind string) []ReviewLine {
	ls := []ReviewLine{}
	if len(decls) < 1 {
		return ls
	}
	// create keys slice in order to later sort consts by their types
	keys := []string{}
	// create types slice in order to be able to separate consts by the type they represent
	types := []string{}
	for name, d := range decls {
		if r := rune(name[0]); r != '_' && unicode.IsUpper(r) {
			keys = append(keys, name)
			if !includesType(types, d.Type) {
				types = append(types, d.Type)
			}
		}
	}
	sort.Strings(keys)
	sort.Strings(types)
	// finalKeys will order const keys by their type
	finalKeys := []string{}
	for _, t := range types {
		for _, k := range keys {
			if t == decls[k].Type {
				finalKeys = append(finalKeys, k)
			}
		}
	}
	for _, t := range types {
		// this token parsing is performed so that const declarations of different types are declared
		// in their own const block to make them easier to click on
		ln := ReviewLine{
			Tokens: []ReviewToken{
				{
					Kind:  TokenKindKeyword,
					Value: kind,
				},
			},
		}
		for _, v := range finalKeys {
			if d := decls[v]; d.Type == t {
				rts := []ReviewToken{
					{
						HasSuffixSpace:        true,
						Kind:                  TokenKindTypeName,
						NavigationDisplayName: d.id,
						Value:                 d.Name(),
					},
					{
						Kind:  TokenKindPunctuation,
						Value: strings.Repeat(" ", maxLen-len(d.Name())),
					},
					{
						Kind:  TokenKindStringLiteral,
						Value: d.value,
					},
				}
				ln.Children = append(ln.Children, ReviewLine{
					LineID: d.ID(),
					Tokens: rts,
				})
			}
		}
		if pvm := c.searchForPossibleValuesMethod(t); pvm != nil {
			ln.Children = append(ln.Children, ReviewLine{})
			ln.Children = append(ln.Children, *pvm)
		}
		ls = append(ls, ln)
	}
	if len(finalKeys) > 0 {
		ls = append(ls, ReviewLine{IsContextEndLine: true})
	}
	return ls
}

func (c *content) searchForPossibleValuesMethod(t string) *ReviewLine {
	for i, f := range c.Funcs {
		if f.Name() == fmt.Sprintf("Possible%sValues", removeNavigatorString(t)) {
			fl := f.MakeReviewLine()
			delete(c.Funcs, i)
			return &fl
		}
	}
	return nil
}

// addFunc adds the specified function declaration to the exports list
// The imports map stores the key value pair for package imports which will be used to identify types.
func (c *content) addFunc(pkg Pkg, f *ast.FuncDecl, imports map[string]string) Func {
	fn := NewFunc(pkg, f, imports)
	name := fn.Name()
	if fn.ReceiverType != "" {
		receiverSig := fn.ReceiverType
		if fn.ReceiverName != "" {
			receiverSig = fn.ReceiverName + " " + receiverSig
		}
		name = fmt.Sprintf("(%s) %s", receiverSig, name)
	}
	c.Funcs[name] = fn
	return fn
}

// addSimpleType adds the specified simple type declaration to the exports list
// The imports map stores the key value pair for package imports which will be used to identify types.
func (c *content) addSimpleType(pkg Pkg, name, packageName string, underlyingType string, imports map[string]string) SimpleType {
	t := NewSimpleType(name, packageName, pkg.translateType(underlyingType, imports))
	c.SimpleTypes[name] = t
	return t
}

// addInterface adds the specified interface type to the exports list.
// The imports map stores the key value pair for package imports which will be used to identify types.
func (c *content) addInterface(source Pkg, name, packageName string, i *ast.InterfaceType, imports map[string]string) Interface {
	in := NewInterface(source, name, packageName, i, imports)
	c.Interfaces[name] = in
	return in
}

// adds the specified struct type to the exports list.
func (c *content) parseInterface() []ReviewLine {
	ls := []ReviewLine{}
	keys := []string{}
	for name := range c.Interfaces {
		if unicode.IsUpper(rune(name[0])) {
			keys = append(keys, name)
		}
	}
	sort.Strings(keys)
	for _, k := range keys {
		il := c.Interfaces[k].MakeReviewLine()
		ls = append(ls, il)
	}
	return ls
}

// addStruct adds the specified struct type to the exports list.
// The imports map stores the key value pair for package imports which will be used to identify types.
func (c *content) addStruct(source Pkg, name, packageName string, ts *ast.TypeSpec, imports map[string]string) Struct {
	s := NewStruct(source, name, packageName, ts, imports)
	c.Structs[name] = s
	return s
}

func (c *content) parseStruct() []ReviewLine {
	ls := []ReviewLine{}
	names := []string{}
	for name := range c.Structs {
		if unicode.IsUpper(rune(name[0])) {
			names = append(names, name)
		}
	}
	sort.Strings(names)
	for _, typeName := range names {
		sl := c.Structs[typeName].MakeReviewLine()

		ctors := c.searchForCtors(typeName)
		methods := c.findMethods(typeName)
		if len(sl.Children) > 0 && (len(ctors) > 0 || len(methods) > 0) {
			// add a blank link between fields and ctors/methods
			sl.Children = append(sl.Children, ReviewLine{})
		}
		if len(ctors) > 0 {
			names := make([]string, 0, len(ctors))
			for name := range ctors {
				names = append(names, name)
			}
			slices.Sort(names)
			for _, name := range names {
				cl := ctors[name].MakeReviewLine()
				cl.RelatedToLine = sl.LineID
				sl.Children = append(sl.Children, cl)
				delete(c.Funcs, name)
			}
		}
		if len(methods) > 0 {
			names := make([]string, 0, len(methods))
			for name := range methods {
				names = append(names, name)
			}
			slices.Sort(names)
			for _, name := range names {
				ml := methods[name].MakeReviewLine()
				ml.RelatedToLine = sl.LineID
				sl.Children = append(sl.Children, ml)
				delete(c.Funcs, name)
			}
		}
		if consts := c.filterDeclarations(typeName, c.Consts); len(consts) > 0 {
			sl.Children = append(sl.Children, c.parseDeclarations(consts, "const")...)
		}
		if vars := c.filterDeclarations(typeName, c.Vars); len(vars) > 0 {
			sl.Children = append(sl.Children, c.parseDeclarations(vars, "var")...)
		}
		ls = append(ls, sl)
		ls = append(ls, ReviewLine{IsContextEndLine: true})
	}
	return ls
}

// searchForCtors searches through exported Funcs for constructors of a type,
// given that type's name. A Func is a constructor of type T when:
// 1. it has no receiver
// 2. its name begins with "New"
// 3. it returns T or *T
func (c *content) searchForCtors(s string) map[string]Func {
	ctors := map[string]Func{}
	for key, f := range c.Funcs {
		if f.ReceiverType != "" || !strings.HasPrefix(f.Name(), "New") {
			continue
		}
		for _, rt := range f.Returns {
			if before, _, found := strings.Cut(rt, "["); found {
				// ignore type parameters when matching
				rt = before
			}
			rt = removeNavigatorString(rt)
			if rt == s || rt == "*"+s {
				ctors[key] = f
				delete(c.Funcs, key)
			}
		}
	}
	return ctors
}

// filterDeclarations returns a subset of decls containing only items matching the specified type, deleting them from the given map
func (c *content) filterDeclarations(typ string, decls map[string]Declaration) map[string]Declaration {
	results := map[string]Declaration{}
	for name, decl := range decls {
		t := removeNavigatorString(decl.Type)
		if typ == t {
			results[name] = decl
			delete(decls, name)
		}
	}
	return results
}

// findMethods takes the name of the receiver and looks for Funcs that are methods on that receiver.
func (c *content) findMethods(s string) map[string]Func {
	methods := map[string]Func{}
	for key, fn := range c.Funcs {
		name := fn.Name()
		if unicode.IsLower(rune(name[0])) {
			continue
		}
		_, n := getReceiver(key)
		if before, _, found := strings.Cut(n, "["); found {
			// ignore type parameters when matching receivers to types
			n = before
		}
		n = removeNavigatorString(n)
		if s == n || "*"+s == n {
			methods[key] = fn
		}
	}
	return methods
}

// searchForMethods takes the name of the receiver and looks for Funcs that are methods on that receiver.
func (c *content) searchForMethods(s string) []ReviewLine {
	lines := []ReviewLine{}
	methods := c.findMethods(s)
	methodNames := []string{}
	for key := range methods {
		methodNames = append(methodNames, key)
	}
	sort.Strings(methodNames)
	for _, name := range methodNames {
		fn := methods[name]
		lines = append(lines, fn.MakeReviewLine())
		delete(c.Funcs, name)
	}
	return lines
}

// receiverRegex captures a receiver's type and optional name
var receiverRegex = regexp.MustCompile(`\((\w*)?(?: ?(\*?\w.*)\))?`)

// getReceiver returns the components of the receiver on a method signature
// i.e.: (c *Foo) Bar(s string) will return "c" and "Foo".
func getReceiver(s string) (string, string) {
	name, typ := "", ""
	t := receiverRegex.FindStringSubmatch(s)
	if t != nil {
		if t[2] == "" {
			// nameless receiver e.g., (Foo)
			typ = t[1]
		} else {
			name = t[1]
			typ = t[2]
		}
	}
	return name, typ
}

// isOnUnexportedMember returns true for method signatures with unexported receivers such as
// "(ep *entityPager[TFeed, T, TOutput]) Fetcher(ctx context.Context) ([]TOutput, error)"
func isOnUnexportedMember(s string) bool {
	r := regexp.MustCompile(`\([a-zA-Z]* \*?[a-z]+.*\)`)
	return r.MatchString(s)
}

// isExampleOrTest returns true if the string passed in begins with "Example" or "Test", this is used
// to help exclude these functions from the API view output
func isExampleOrTest(s string) bool {
	return strings.Contains(s, "Example") || strings.Contains(s, "Test")
}

func (c *content) parseFunc() []ReviewLine {
	lns := []ReviewLine{}
	keys := make([]string, 0, len(c.Funcs))
	for key, fn := range c.Funcs {
		name := fn.Name()
		if !(isOnUnexportedMember(key) || isExampleOrTest(name) || unicode.IsLower(rune(name[0]))) {
			keys = append(keys, key)
		}
	}
	sort.Strings(keys)
	for _, k := range keys {
		lns = append(lns, c.Funcs[k].MakeReviewLine())
	}
	return lns
}

// generateNavChildItems will loop through all the consts, interfaces, structs and global functions
// to create the navigation items that will be displayed in the API view.
// For consts, a navigation item will be by const type.
// For interfaces, a navigation item will point to the interface definition.
// For structs, a navigation item will only point to the struct definition and not methods or functions related to the struct.
// For funcs, global funcs that are not constructors for any structs will have a direct navigation item.
func (c *content) generateNavChildItems() []NavigationItem {
	items := []NavigationItem{}
	for _, cst := range c.Consts {
		if cst.Exported() {
			items = append(items, NavigationItem{
				Text:         cst.Name(),
				NavigationID: cst.ID(),
				ChildItems:   []NavigationItem{},
				Tags: &map[string]string{
					"TypeKind": "enum",
				},
			})
		}
	}
	for _, f := range c.Funcs {
		if f.Exported() {
			items = append(items, NavigationItem{
				Text:         f.Name(),
				NavigationID: f.ID(),
				ChildItems:   []NavigationItem{},
				Tags: &map[string]string{
					"TypeKind": "delegate",
				},
			})
		}
	}
	for _, i := range c.Interfaces {
		if i.Exported() {
			items = append(items, NavigationItem{
				Text:         i.Name(),
				NavigationID: i.ID(),
				ChildItems:   []NavigationItem{},
				Tags: &map[string]string{
					"TypeKind": "interface",
				},
			})
		}
	}
	for _, n := range c.SimpleTypes {
		if n.Exported() {
			items = append(items, NavigationItem{
				Text:         n.Name(),
				NavigationID: n.ID(),
				ChildItems:   []NavigationItem{},
				Tags: &map[string]string{
					"TypeKind": "struct",
				},
			})
		}
	}
	for _, s := range c.Structs {
		if s.Exported() {
			items = append(items, NavigationItem{
				Text:         s.Name(),
				NavigationID: s.ID(),
				ChildItems:   []NavigationItem{},
				Tags: &map[string]string{
					"TypeKind": "class",
				},
			})
		}
	}
	for _, v := range c.Vars {
		if v.Exported() {
			items = append(items, NavigationItem{
				Text:         v.Name(),
				NavigationID: v.ID(),
				ChildItems:   []NavigationItem{},
				Tags: &map[string]string{
					"TypeKind": "unknown",
				},
			})
		}
	}
	sort.Slice(items, func(i, j int) bool {
		return items[i].Text < items[j].Text
	})
	return items
}

// removeNavigatorString help to remove any navigator ("<xxx>") in types for easy comparison
func removeNavigatorString(str string) string {
	if i := strings.Index(str, ">"); i > 0 {
		str = str[i+1:]
	}
	return str
}
