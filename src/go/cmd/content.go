// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"fmt"
	"go/ast"
	"go/token"
	"regexp"
	"sort"
	"strings"
	"unicode"
	"unicode/utf8"
)

// skip adding the const type in the token list
const skip = "Untyped const"

// content defines the set of exported constants, funcs, and structs.
type content struct {
	// the list of exported constants.
	// key is the exported name, value is its type and value.
	Consts map[string]Declaration `json:"consts,omitempty"`

	// the list of exported functions and methods.
	// key is the exported name, for methods it's prefixed with the receiver type (e.g. "Type.Method").
	// value contains the list of params and return types.
	Funcs map[string]Func `json:"funcs,omitempty"`

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

// adds const and var declaration to the exports list
func (c *content) addGenDecl(pkg Pkg, g *ast.GenDecl) {
	for _, s := range g.Specs {
		vs := s.(*ast.ValueSpec)
		v := getExprValue(pkg, vs.Values[0])
		if v == "" {
			fmt.Printf("WARNING: failed to determine value for %s\n", pkg.getText(vs.Pos(), vs.End()))
			continue
		}
		decl := Declaration{Value: v}
		// Type is nil for untyped consts
		if vs.Type != nil {
			switch x := vs.Type.(type) {
			case *ast.Ident:
				// const ETagAny ETag = "*"
				// const PeekLock ReceiveMode = internal.PeekLock
				decl.Type = x.Name
			case *ast.SelectorExpr:
				// const LogCredential log.Classification = "Credential"
				decl.Type = x.Sel.Name
			default:
				fmt.Printf("WARNING: unhandled constant type %s\n", pkg.getText(vs.Type.Pos(), vs.Type.End()))
			}
		} else if _, ok := vs.Values[0].(*ast.CallExpr); ok {
			// const FooConst = Foo("value")
			// var Foo = NewFoo()
			// TODO: determining the type here requires finding the definition of the called function. We may
			// not have encountered that yet, so we would need to set the types of these declarations after
			// traversing the entire AST.
		} else if cl, ok := vs.Values[0].(*ast.CompositeLit); ok {
			// var AzureChina = Configuration{ ... }
			decl.Type = pkg.getText(cl.Type.Pos(), cl.Type.End())
		} else {
			// implicitly typed const
			decl.Type = skip
		}
		// TODO handle multiple names like "var a, b = 42"
		switch g.Tok {
		case token.CONST:
			c.Consts[vs.Names[0].Name] = decl
		case token.VAR:
			c.Vars[vs.Names[0].Name] = decl
		default:
			fmt.Printf("WARNING: unexpected declaration kind %v", vs.Names[0].Obj.Kind)
		}
	}
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
	case *ast.Ident:
		// const DefaultLinkBatching = false
		return x.Name
	case *ast.SelectorExpr:
		// const ModeUnsettled = encoding.ModeUnsettled
		return pkg.getText(x.Pos(), x.End())
	case *ast.UnaryExpr:
		// const FooConst = -1
		return pkg.getText(x.Pos(), x.End())
	}
	fmt.Printf("WARNING: unhandled constant value %s\n", pkg.getText(expr.Pos(), expr.End()))
	return ""
}

func includesType(s []string, t string) bool {
	for _, j := range s {
		if j == t {
			return true
		}
	}
	return false
}

func (c *content) parseSimpleType(tokenList *[]Token) {
	keys := make([]string, 0, len(c.SimpleTypes))
	for key := range c.SimpleTypes {
		keys = append(keys, key)
	}
	sort.Strings(keys)
	for _, name := range keys {
		v := c.SimpleTypes[name]
		n := name
		makeToken(&n, &n, "type", keyword, tokenList)
		makeToken(nil, nil, " ", whitespace, tokenList)
		makeToken(nil, nil, *v.Name, typeName, tokenList)
		makeToken(nil, nil, " ", whitespace, tokenList)
		makeToken(nil, nil, *v.UnderlyingType, text, tokenList)
		makeToken(nil, nil, "", newline, tokenList)
		makeToken(nil, nil, "", newline, tokenList)
		c.searchForMethods(name, tokenList)
	}
}

func (c *content) parseConst(tokenList *[]Token) {
	c.parseDeclarations(c.Consts, "const", tokenList)
}

func (c *content) parseVar(tokenList *[]Token) {
	c.parseDeclarations(c.Vars, "var", tokenList)
}

func (c *content) parseDeclarations(decls map[string]Declaration, kind string, tokenList *[]Token) {
	if len(decls) < 1 {
		return
	}
	// create keys slice in order to later sort consts by their types
	keys := []string{}
	// create types slice in order to be able to separate consts by the type they represent
	types := []string{}
	for i, s := range decls {
		keys = append(keys, i)
		if !includesType(types, s.Type) {
			types = append(types, s.Type)
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
		n := t
		makeToken(&n, nil, kind, keyword, tokenList)
		makeToken(nil, nil, " ", whitespace, tokenList)
		makeToken(nil, nil, "(", punctuation, tokenList)
		makeToken(nil, nil, "", 1, tokenList)
		for _, v := range finalKeys {
			if decls[v].Type == t {
				makeDeclarationTokens(&v, decls[v], tokenList)
			}
		}
		makeToken(nil, nil, ")", punctuation, tokenList)
		makeToken(nil, nil, "", 1, tokenList)
		makeToken(nil, nil, "", newline, tokenList)
		c.searchForPossibleValuesMethod(t, tokenList)
	}
}

func (c *content) searchForPossibleValuesMethod(t string, tokenList *[]Token) {
	for i, f := range c.Funcs {
		if i == fmt.Sprintf("Possible%sValues", t) {
			makeFuncTokens(&i, f, tokenList)
			delete(c.Funcs, i)
			return
		}
	}
}

// adds the specified function declaration to the exports list
func (c *content) addFunc(pkg Pkg, f *ast.FuncDecl) {
	// create a method sig, for methods it's a combination of the receiver type
	// with the function name e.g. "FooReceiver.Method", else just the function name.
	sig := ""
	if f.Recv != nil {
		receiver := pkg.getText(f.Recv.List[0].Type.Pos(), f.Recv.List[0].Type.End())
		if !isExported(receiver) {
			// skip adding methods on unexported receivers
			return
		}
		name := ""
		if len(f.Recv.List[0].Names) != 0 {
			name = f.Recv.List[0].Names[0].Name + " "
		}
		sig = fmt.Sprintf("(%s%s) ", name, receiver)
	}
	sig += f.Name.Name
	c.Funcs[sig] = pkg.buildFunc(f.Type)
}

func (c *content) addSimpleType(pkg Pkg, name string, underlyingType string) {
	c.SimpleTypes[name] = SimpleType{Name: &name, UnderlyingType: &underlyingType}
}

// adds the specified interface type to the exports list.
func (c *content) addInterface(pkg Pkg, name string, i *ast.InterfaceType) {
	in := Interface{Methods: map[string]Func{}}
	if i.Methods != nil {
		for _, m := range i.Methods.List {
			if len(m.Names) > 0 {
				n := m.Names[0].Name
				f := pkg.buildFunc(m.Type.(*ast.FuncType))
				in.Methods[n] = f
			} else {
				n := pkg.getText(m.Type.Pos(), m.Type.End())
				in.EmbeddedInterfaces = append(in.EmbeddedInterfaces, n)
			}
		}
	}
	c.Interfaces[name] = in
}

// adds the specified struct type to the exports list.
func (c *content) parseInterface(tokenList *[]Token) {
	keys := []string{}
	for s := range c.Interfaces {
		keys = append(keys, s)
	}
	sort.Strings(keys)
	for _, k := range keys {
		makeInterfaceTokens(&k, c.Interfaces[k].EmbeddedInterfaces, c.Interfaces[k].Methods, tokenList)
	}
}

// adds the specified struct type to the exports list.
func (c *content) addStruct(pkg Pkg, name string, ts *ast.TypeSpec) {
	sd := Struct{}
	if ts.TypeParams != nil {
		sd.TypeParams = make([]string, 0, len(ts.TypeParams.List))
		pkg.translateFieldList(ts.TypeParams.List, func(param *string, constraint string) {
			sd.TypeParams = append(sd.TypeParams, strings.TrimRight(*param+" "+constraint, " "))
		})
	}
	pkg.translateFieldList(ts.Type.(*ast.StructType).Fields.List, func(n *string, t string) {
		if n == nil {
			sd.AnonymousFields = append(sd.AnonymousFields, t)
		} else {
			if sd.Fields == nil {
				sd.Fields = map[string]string{}
			}
			sd.Fields[*n] = t
		}
	})
	sort.Strings(sd.AnonymousFields)
	c.Structs[name] = sd
}

// adds the specified struct type to the exports list.
func (c *content) parseStruct(tokenList *[]Token) {
	keys := make([]string, 0, len(c.Structs))
	for k := range c.Structs {
		keys = append(keys, k)
	}
	sort.Strings(keys)
	for _, k := range keys {
		makeStructTokens(&k, c.Structs[k], tokenList)
		c.searchForCtors(k, tokenList)
		c.searchForMethods(k, tokenList)
	}
}

// searchForCtors will search through all of the exported Funcs for a constructor for the name of the
// type that is passed as a param.
func (c *content) searchForCtors(s string, tokenList *[]Token) {
	for i, f := range c.Funcs {
		n := getCtorName(i)
		if s == n {
			makeFuncTokens(&i, f, tokenList)
			delete(c.Funcs, i)
			return
		}
	}
}

// searchForMethods takes the name of the receiver and looks for Funcs that are methods on that receiver.
func (c *content) searchForMethods(s string, tokenList *[]Token) {
	methods := map[string]Func{}
	methodNames := []string{}
	for name, fn := range c.Funcs {
		_, n := getReceiver(name)
		if before, _, found := strings.Cut(n, "["); found {
			// ignore type parameters when matching receivers to types
			n = before
		}
		if s == n || "*"+s == n {
			methods[name] = fn
			methodNames = append(methodNames, name)
			delete(c.Funcs, name)
		}
		if isOnUnexportedMember(name) || isExampleOrTest(name) {
			delete(c.Funcs, name)
		}
	}
	sort.Strings(methodNames)
	for _, name := range methodNames {
		fn := methods[name]
		v, n := getReceiver(name)
		isPointer := false
		if strings.HasPrefix(n, "*") {
			n = n[1:]
			isPointer = true
		}
		makeMethodTokens(v, n, isPointer, getMethodName(name), fn, tokenList)
	}
}

// getCtorName returns the name of a constructor without the New prefix.
// TODO improve this to also check the return statement on the constructor to make sure it does in fact only
// return the name of constructors and not other functions that begin with New
func getCtorName(s string) string {
	if strings.HasPrefix(s, "New") {
		ctor := s[3:]
		return ctor
	}
	return ""
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

// getMethodName expects a method signature in the param s and removes the receiver portion of the
// method signature before returning the method name.
func getMethodName(s string) string {
	pos := strings.Index(s, ")")
	// return the string after the first ) and add an extra index to omit the space after the receiver
	return s[pos+2:]
}

// isOnUnexportedMember checks for method signatures that are on unexported types,
// it will return true if the method is unexported.
func isOnUnexportedMember(s string) bool {
	r := regexp.MustCompile(`(\(([a-z|A-Z]{1}) (\*){0,1}([a-z]+)([a-z|A-Z]*)\))`)
	return r.MatchString(s)
	// for _, l := range s {
	// 	if unicode.IsLetter(l) {
	// 		return string(l) == strings.ToLower(string(l))
	// 	}
	// }
	// return false
}

// isExampleOrTest returns true if the string passed in begins with "Example" or "Test", this is used
// to help exclude these functions from the API view output
func isExampleOrTest(s string) bool {
	return strings.Contains(s, "Example") || strings.Contains(s, "Test")
}

func (c *content) parseFunc(tokenList *[]Token) {
	keys := make([]string, 0, len(c.Funcs))
	for k := range c.Funcs {
		keys = append(keys, k)
	}
	sort.Strings(keys)
	for i, k := range keys {
		if isOnUnexportedMember(k) || isExampleOrTest(k) {
			copy(keys[i:], keys[i+1:])
			keys[len(keys)-1] = ""
			keys = keys[:len(keys)-1]
			delete(c.Funcs, k)
		}
	}
	for _, k := range keys {
		makeFuncTokens(&k, c.Funcs[k], tokenList)
	}
}

// generateNavChildItems will loop through all the consts, interfaces, structs and global functions
// to create the navigation items that will be displayed in the API view.
// For consts, a navigation item will be by const type.
// For interfaces, a navigation item will point to the interface definition.
// For structs, a navigation item will only point to the struct definition and not methods or functions related to the struct.
// For funcs, global funcs that are not constructors for any structs will have a direct navigation item.
func (c *content) generateNavChildItems() []Navigation {
	childItems := []Navigation{}
	types := []string{}
	keys := []string{}
	for _, s := range c.Consts {
		keys = append(keys, s.Type)
	}
	sort.Strings(keys)
	for _, k := range keys {
		if !includesType(types, k) {
			types = append(types, k)
			temp := k
			childItems = append(childItems, Navigation{
				Text:         &temp,
				NavigationId: &temp,
				ChildItems:   []Navigation{},
				Tags: &map[string]string{
					"TypeKind": "enum",
				},
			})
		}
	}
	keys = []string{}
	for i := range c.Interfaces {
		keys = append(keys, i)
	}
	sort.Strings(keys)
	for k := range keys {
		childItems = append(childItems, Navigation{
			Text:         &keys[k],
			NavigationId: &keys[k],
			ChildItems:   []Navigation{},
			Tags: &map[string]string{
				"TypeKind": "interface",
			},
		})
	}
	keys = []string{}
	for i := range c.Structs {
		keys = append(keys, i)
	}
	sort.Strings(keys)
	clientsFirst := []string{}
	for i, k := range keys {
		if strings.HasSuffix(k, "Client") {
			clientsFirst = append(clientsFirst, k)
			keys = append(keys[:i], keys[i+1:]...)
		}
	}
	clientsFirst = append(clientsFirst, keys...)
	for k := range clientsFirst {
		childItems = append(childItems, Navigation{
			Text:         &clientsFirst[k],
			NavigationId: &clientsFirst[k],
			ChildItems:   []Navigation{},
			Tags: &map[string]string{
				"TypeKind": "struct",
			},
		})
	}
	keys = []string{}
	for i := range c.Funcs {
		keys = append(keys, i)
	}
	sort.Strings(keys)
	for k := range keys {
		childItems = append(childItems, Navigation{
			Text:         &keys[k],
			NavigationId: &keys[k],
			ChildItems:   []Navigation{},
			Tags: &map[string]string{
				"TypeKind": "unknown",
			},
		})
	}
	return childItems
}

func isExported(name string) bool {
	if string(name[0]) == "*" {
		name = name[1:]
	}
	ch, _ := utf8.DecodeRuneInString(name)
	return unicode.IsUpper(ch)
}
