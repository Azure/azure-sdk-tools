// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"fmt"
	"go/ast"
	"regexp"
	"sort"
	"strings"
	"unicode"
	"unicode/utf8"
)

// skip adding the const type in the token list
const skip = "Untyped const"

// newContent returns an initialized Content object.
func newContent() content {
	return content{
		Consts:     make(map[string]Const),
		Funcs:      make(map[string]Func),
		Interfaces: make(map[string]Interface),
		Structs:    make(map[string]Struct),
	}
}

// isEmpty returns true if there is no content in any of the fields.
func (c content) isEmpty() bool {
	return len(c.Consts) == 0 && len(c.Funcs) == 0 && len(c.Interfaces) == 0 && len(c.Structs) == 0
}

// adds the specified const declaration to the exports list
func (c *content) addConst(pkg pkg, g *ast.GenDecl) {
	for _, s := range g.Specs {
		co := Const{}
		vs := s.(*ast.ValueSpec)
		v := ""
		// Type is nil for untyped consts
		if vs.Type != nil {
			switch x := vs.Type.(type) {
			case *ast.Ident:
				co.Type = x.Name
				v = vs.Values[0].(*ast.BasicLit).Value
			case *ast.SelectorExpr:
				co.Type = x.Sel.Name
				v = vs.Values[0].(*ast.BasicLit).Value
			default:
				panic(fmt.Sprintf("wrong type %T", vs.Type))
			}

		} else {
			// get the type from the token type
			if bl, ok := vs.Values[0].(*ast.BasicLit); ok {
				co.Type = skip
				v = bl.Value
			} else if ce, ok := vs.Values[0].(*ast.CallExpr); ok {
				// const FooConst = FooType("value")
				co.Type = pkg.getText(ce.Fun.Pos(), ce.Fun.End())
				v = pkg.getText(ce.Args[0].Pos(), ce.Args[0].End())
			} else if ce, ok := vs.Values[0].(*ast.BinaryExpr); ok {
				// const FooConst = "value" + Bar
				co.Type = skip
				v = pkg.getText(ce.X.Pos(), ce.Y.End())
			} else if ce, ok := vs.Values[0].(*ast.UnaryExpr); ok {
				// const FooConst = -1
				co.Type = skip
				v = pkg.getText(ce.Pos(), ce.End())
			} else {
				panic("unhandled case for adding constant")
			}
		}
		co.Value = v
		c.Consts[vs.Names[0].Name] = co
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

func (c *content) parseConst(tokenList *[]Token) {
	if len(c.Consts) > 0 {
		// create keys slice in order to later sort consts by their types
		keys := []string{}
		// create types slice in order to be able to separate consts by the type they represent
		types := []string{}
		for i, s := range c.Consts {
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
				if t == c.Consts[k].Type {
					finalKeys = append(finalKeys, k)
				}
			}
		}
		for _, t := range types {
			// this token parsing is performed so that const declarations of different types are declared
			// in their own const block to make them easier to click on
			n := t
			makeToken(nil, nil, "", 1, tokenList)
			makeToken(nil, nil, " ", whitespace, tokenList)
			makeToken(nil, nil, "", 1, tokenList)
			makeToken(&n, nil, "const", keyword, tokenList)
			makeToken(nil, nil, " ", whitespace, tokenList)
			makeToken(nil, nil, "(", punctuation, tokenList)
			makeToken(nil, nil, "", 1, tokenList)
			for _, v := range finalKeys {
				if c.Consts[v].Type == t {
					makeConstTokens(&v, c.Consts[v], tokenList)
				}
			}
			makeToken(nil, nil, ")", punctuation, tokenList)
			makeToken(nil, nil, "", 1, tokenList)

			c.searchForPossibleValuesMethod(t, tokenList)
			c.searchForMethods(t, tokenList)
		}
	}

}

func (c *content) searchForPossibleValuesMethod(t string, tokenList *[]Token) {
	for i, f := range c.Funcs {
		if i == fmt.Sprintf("Possible%sValues", t) {
			makeFuncTokens(&i, f.Params, f.Returns, f.ReturnsNum, tokenList)
			delete(c.Funcs, i)
			return
		}
	}
}

// adds the specified function declaration to the exports list
func (c *content) addFunc(pkg pkg, f *ast.FuncDecl) {
	// create a method sig, for methods it's a combination of the receiver type
	// with the function name e.g. "FooReceiver.Method", else just the function name.
	sig := ""
	if f.Recv != nil {
		receiver := pkg.getText(f.Recv.List[0].Type.Pos(), f.Recv.List[0].Type.End())
		if !isExported(receiver) {
			// skip adding methods on unexported receivers
			return
		}
		sig = "(" + f.Recv.List[0].Names[0].Name + " "
		sig += receiver
		// CP: changed to space, was a period before
		sig += ") "
	}
	sig += f.Name.Name
	c.Funcs[sig] = pkg.buildFunc(f.Type)
}

// adds the specified interface type to the exports list.
func (c *content) addInterface(pkg pkg, name string, i *ast.InterfaceType) {
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
func (c *content) addStruct(pkg pkg, name string, s *ast.StructType) {
	sd := Struct{}
	// assumes all struct types have fields
	pkg.translateFieldList(s.Fields.List, func(n *string, t string) {
		if n == nil {
			sd.AnonymousFields = append(sd.AnonymousFields, t)
		} else {
			if sd.Fields == nil {
				sd.Fields = map[string]string{}
			}
			sd.Fields[*n] = t
		}
	})
	c.Structs[name] = sd
}

// adds the specified struct type to the exports list.
func (c *content) parseStruct(tokenList *[]Token) {
	keys := make([]string, 0, len(c.Structs))
	for k := range c.Structs {
		keys = append(keys, k)
	}
	sort.Strings(keys)
	clients := []string{}
	for i, k := range keys {
		if strings.HasSuffix(k, "Client") {
			clients = append(clients, k)
			keys = append(keys[:i], keys[i+1:]...)
		}
	}
	clients = append(clients, keys...)
	for _, k := range clients {
		makeStructTokens(&k, c.Structs[k].AnonymousFields, c.Structs[k].Fields, tokenList)
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
			makeFuncTokens(&i, f.Params, f.Returns, f.ReturnsNum, tokenList)
			delete(c.Funcs, i)
			return
		}
	}
}

// searchForMethods takes the name of the receiver and looks for Funcs that are methods on that receiver.
func (c *content) searchForMethods(s string, tokenList *[]Token) {
	for i, f := range c.Funcs {
		v, n := getReceiverName(i)
		isPointer := false
		if strings.HasPrefix(n, "*") {
			n = n[1:]
			isPointer = true
		}
		if s == n {
			makeMethodTokens(v, n, isPointer, getMethodName(i), f.Params, f.Returns, f.ReturnsNum, tokenList)
			delete(c.Funcs, i)
		}
		if isOnUnexportedMember(i) || isExampleOrTest(i) {
			delete(c.Funcs, i)
		}
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

// getReceiverName returns the components of the receiver on a method signature
// i.e.: (c *Foo) Bar(s string) will return "c" and "Foo".
func getReceiverName(s string) (receiverVar string, receiver string) {
	if strings.HasPrefix(s, "(") {
		parts := strings.Split(s[:strings.Index(s, ")")], " ")
		receiverVar = parts[0][1:]
		receiver = parts[1]
		return
	}
	return "", ""
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
		makeToken(nil, nil, "", newline, tokenList)
		makeToken(nil, nil, " ", whitespace, tokenList)
		makeToken(nil, nil, "", newline, tokenList)
		makeFuncTokens(&k, c.Funcs[k].Params, c.Funcs[k].Returns, c.Funcs[k].ReturnsNum, tokenList)
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
